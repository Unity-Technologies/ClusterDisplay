using System.Diagnostics;
using System.Net;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class LaunchServiceExtension
    {
        public static void AddLaunchService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchService>();
        }
    }

    /// <summary>
    /// Service responsible for managing launches (preparing, launching and stopping launchpads).
    /// </summary>
    public class LaunchService
    {
        public LaunchService(ILogger<LaunchService> logger,
            IHostApplicationLifetime applicationLifetime,
            ConfigService configService,
            StatusService statusService,
            AssetsService assetsService,
            ComplexesService complexesService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService,
            LaunchPadsStatusService launchPadsStatusService,
            CapcomUplinkService capcomUplink,
            IncrementalCollectionCatalogService incrementalCollectionCatalogService)
        {
            m_Logger = logger;
            m_ConfigService = configService;
            m_StatusService = statusService;
            m_AssetsService = assetsService;
            m_ComplexesService = complexesService;
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;
            m_LaunchPadsStatusService = launchPadsStatusService;
            m_CapcomUplink = capcomUplink;

            m_ConfigService.ValidateNew += ValidateNewConfig;

            m_Manager = new(m_Logger, new());

            incrementalCollectionCatalogService.Register("currentMission/launchParametersForReview",
                RegisterForReviewChangesInCollection, GetReviewIncrementalUpdatesAsync);

            applicationLifetime.ApplicationStopping.Register(StopAtExit);
        }

        /// <summary>
        /// Launch the current launch configuration in <see cref="CurrentMissionLaunchConfigurationService"/>.
        /// </summary>
        public async Task<(HttpStatusCode code, string errorMessage)> LaunchAsync()
        {
            using var lockedStatus = await m_StatusService.LockAsync();
            if (lockedStatus.Value.State != State.Idle)
            {
                return (HttpStatusCode.Conflict, "There is already a mission in progress.");
            }

            // Get all the information we need for a launch
            LaunchManager.LaunchManifest manifest = new();
            using (var lockedLaunchConfiguration = await m_CurrentMissionLaunchConfigurationService.LockAsync())
            {
                manifest.LaunchConfiguration = lockedLaunchConfiguration.Value.DeepClone();
            }

            using (var lockedAssets = await m_AssetsService.Manager.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(manifest.LaunchConfiguration.AssetId, out var asset))
                {
                    return (HttpStatusCode.BadRequest, $"Cannot find asset " +
                        $"({manifest.LaunchConfiguration.AssetId}) for current launch configuration.");
                }
                manifest.Asset = asset.DeepClone();
            }

            using (var lockedComplexes = await m_ComplexesService.Manager.GetLockedReadOnlyAsync())
            {
                manifest.Complexes = lockedComplexes.Value.Values.Select(c => c.DeepClone());
            }

            manifest.Config = m_ConfigService.Current;

            // Launch!
            using (var lockedStatuses = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                manifest.LaunchPadsStatus = lockedStatuses.Value;
                var launchTask = m_Manager.LaunchAsync(manifest);
                if (launchTask.IsCompleted)
                {
                    // Mission was executed super quickly (most likely it failed), however we are already done, so
                    // no need to update the state, simply conclude the LaunchManager and we are done.
                    m_Manager.Conclude(manifest.LaunchPadsStatus);
                    return (HttpStatusCode.OK, "");
                }

                lockedStatus.Value.State = State.Preparing;
                lockedStatus.Value.SignalChanges();

                _ = MonitorMission(launchTask);

                return (HttpStatusCode.Accepted, "");
            }
        }

        /// <summary>
        /// Initiate stop of a currently running mission.
        /// </summary>
        public async Task<(HttpStatusCode code, string errorMessage)> StopAsync()
        {
            using var lockedStatus = await m_StatusService.LockAsync();
            if (lockedStatus.Value.State == State.Idle)
            {
                return (HttpStatusCode.OK, "");
            }

            _ = m_Manager.LandAsync(() =>
            {
                using var lockedCapcom = m_CapcomUplink.LockAsync().Result;
                lockedCapcom.Value.ProceedWithLanding = true;
                lockedCapcom.Value.SignalChanges();
            });

            // MonitorMission will restore the state to idle when all launchpads are stopped.
            return (HttpStatusCode.Accepted, "");
        }

        /// <summary>
        /// Returns the incremental to the <see cref="IncrementalCollection{LaunchParameterForReview}"/> update from
        /// the specified version.
        /// </summary>
        /// <param name="fromVersion">Version number from which we want to get the incremental update.</param>
        public IncrementalCollectionUpdate<LaunchParameterForReview> GetLaunchParametersForReviewUpdate(
            ulong fromVersion) => m_Manager.GetLaunchParametersForReviewUpdate(fromVersion);

        /// <summary>
        /// Returns the current list of <see cref="LaunchParameterForReview"/>.
        /// </summary>
        public List<LaunchParameterForReview> GetLaunchParametersForReview()
            => m_Manager.GetLaunchParametersForReview();

        /// <summary>
        /// Returns the requested <see cref="LaunchParameterForReview"/>.
        /// </summary>
        public LaunchParameterForReview GetLaunchParameterForReview(Guid id)
            => m_Manager.GetLaunchParameterForReview(id);

        /// <summary>
        /// Update a <see cref="LaunchParameterForReview"/> in the list to be reviewed.
        /// </summary>
        /// <param name="update">The update to perform</param>
        public void UpdateLaunchParameterForReview(LaunchParameterForReview update)
            => m_Manager.UpdateLaunchParameterForReview(update);

        /// <summary>
        /// Validate a new mission control configuration
        /// </summary>
        /// <param name="survey">Survey to fill about the  validity of the new configuration.</param>
        static void ValidateNewConfig(ConfigService.ConfigChangeSurvey survey)
        {
            if (survey.Proposed.LaunchPadFeedbackTimeoutSec <= 0)
            {
                survey.Reject("launchPadFeedbackTimeoutSec must be > 0");
            }
        }

        /// <summary>
        /// Monitor mission progress and update states accordingly.
        /// </summary>
        /// <param name="launchTask">Task representing the launch that will complete when all the launchpads are done
        /// (either something failed or the launched payload loaded and concluded their execution).</param>
        async Task MonitorMission(Task launchTask)
        {
            State statusState = State.Preparing;
            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                Debug.Assert(lockedStatus.Value.State == State.Preparing);
            }

            await m_Manager.Launched;

            while (!launchTask.IsCompleted)
            {
                // Update the current state based on the number of running launchpads.
                Task runningLaunchPadsChanged = m_Manager.RunningLaunchPadsChanged;
                State newState = m_Manager.RunningLaunchPads < m_Manager.LaunchPadsCount ? State.Failure : State.Launched;
                if (newState == State.Failure && statusState == State.Launched)
                {
                    // We are about to enter failure state, just check that we are not landing, if that is the case
                    // then everything is normal.
                    using var lockedCapcom = m_CapcomUplink.LockAsync().Result;
                    if (lockedCapcom.Value.ProceedWithLanding)
                    {
                        newState = statusState;
                    }
                }
                if (newState != statusState)
                {
                    using var lockedStatus = await m_StatusService.LockAsync();
                    lockedStatus.Value.State = newState;
                    lockedStatus.Value.SignalChanges();
                    statusState = newState;
                }

                // Wait for something to change
                await Task.WhenAny(launchTask, runningLaunchPadsChanged);
            }

            using (var lockedStatuses = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                m_Manager.Conclude(lockedStatuses.Value);
            }

            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                Debug.Assert(lockedStatus.Value.State == statusState);
                lockedStatus.Value.State = State.Idle;
                lockedStatus.Value.SignalChanges();

                using (var lockedCapcom = m_CapcomUplink.LockAsync().Result)
                {
                    lockedCapcom.Value.ProceedWithLanding = false;
                    lockedCapcom.Value.SignalChanges();
                }
            }
        }

        /// <summary>
        /// Stop anything running when we receive the exit signal
        /// </summary>
        void StopAtExit()
        {
            using var lockedStatus = m_StatusService.LockAsync().Result;
            if (lockedStatus.Value.State != State.Idle)
            {
                m_Manager.Stop();
            }
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to register callback to detect changes in the
        /// collection.
        /// </summary>
        /// <param name="toRegister">The callback to register</param>
        void RegisterForReviewChangesInCollection(Action<IReadOnlyIncrementalCollection> toRegister)
        {
            m_Manager.RegisterForLaunchParametersForReviewChanges(toRegister);
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to get an incremental update from the specified
        /// version.
        /// </summary>
        /// <param name="fromVersion">Version number from which we want to get the incremental update.</param>
        Task<object?> GetReviewIncrementalUpdatesAsync(ulong fromVersion)
        {
            var ret = m_Manager.GetLaunchParametersForReviewUpdate(fromVersion);
            return Task.FromResult<object?>(ret.IsEmpty ? null : ret);
        }

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        readonly ILogger m_Logger;
        readonly ConfigService m_ConfigService;
        readonly StatusService m_StatusService;
        readonly AssetsService m_AssetsService;
        readonly ComplexesService m_ComplexesService;
        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
        readonly LaunchPadsStatusService m_LaunchPadsStatusService;
        readonly CapcomUplinkService m_CapcomUplink;

        /// <summary>
        /// The manager doing the launch heavy lifting.
        /// </summary>
        readonly LaunchManager m_Manager;
    }
}
