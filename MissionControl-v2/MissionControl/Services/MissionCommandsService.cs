using System.Net;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class MissionCommandsServiceExtension
    {
        public static void AddMissionCommandsService(this IServiceCollection services)
        {
            services.AddScoped<MissionCommandsService>();
        }
    }

    /// <summary>
    /// Service responsible for executing <see cref="MissionCommand"/>s.
    /// </summary>
    public class MissionCommandsService
    {
       public MissionCommandsService(ILogger<MissionCommandsService> logger,
            StatusService statusService,
            MissionsService missionsService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfiguration,
            LaunchService launchService)
        {
            m_Logger = logger;
            m_StatusService = statusService;
            m_MissionsService = missionsService;
            m_CurrentMissionLaunchConfiguration = currentMissionLaunchConfiguration;
            m_LaunchService = launchService;
        }

        /// <summary>
        /// Execute the specified <see cref="MissionCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Unknown type of MissionCommand.</exception>
        public Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(MissionCommand command)
        {
            return command switch
            {
                SaveMissionCommand   { } commandOfType => ExecuteAsync(commandOfType),
                LoadMissionCommand   { } commandOfType => ExecuteAsync(commandOfType),
                LaunchMissionCommand { } commandOfType => ExecuteAsync(commandOfType),
                StopMissionCommand   { } commandOfType => ExecuteAsync(commandOfType),
                _ => throw new ArgumentException($"{command.GetType()} is not a supported mission command type.")
            };
        }

        async Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(SaveMissionCommand command)
        {
            if (string.IsNullOrEmpty(command.Name))
            {
                return (HttpStatusCode.BadRequest, "Saved mission name is mandatory");
            }

            MissionDetails toSave = new();
            toSave.Identifier = command.Identifier == Guid.Empty ? Guid.NewGuid() : command.Identifier;
            toSave.CopySavedMissionSummaryProperties(command);
            using (var missionLock = await m_CurrentMissionLaunchConfiguration.LockAsync())
            {
                toSave.LaunchConfiguration = missionLock.Value.DeepClone();
            }
            // TODO: Copy parameters (when support for parameters will be added)
            // TODO: Copy panels (when support for panels will be added)

            await m_MissionsService.Manager.StoreAsync(toSave);

            await m_MissionsService.SaveAsync();

            return (HttpStatusCode.OK, "");
        }

        async Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(LoadMissionCommand command)
        {
            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                if (lockedStatus.Value.State != State.Idle)
                {
                    return (HttpStatusCode.Conflict, "Can only load a mission if the current state of mission control is idle.");
                }

                MissionDetails missionDetails;
                try
                {
                    missionDetails = await m_MissionsService.Manager.GetAsync(command.Identifier);
                }
                catch (KeyNotFoundException)
                {
                    return (HttpStatusCode.BadRequest, $"Cannot find mission {command.Identifier}.");
                }

                using (var missionLock = await m_CurrentMissionLaunchConfiguration.LockAsync())
                {
                    if (!missionLock.Value.Equals(missionDetails.LaunchConfiguration))
                    {
                        missionLock.Value.DeepCopy(missionDetails.LaunchConfiguration);
                        missionLock.Value.ObjectChanged();
                    }
                }
                // TODO: Copy parameters (when support for parameters will be added)
                // TODO: Copy panels (when support for panels will be added)
            }

            return (HttpStatusCode.OK, "");
        }

        // ReSharper disable once UnusedParameter.Local
        Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(LaunchMissionCommand command)
        {
            return m_LaunchService.LaunchAsync();
        }

        // ReSharper disable once UnusedParameter.Local
        Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(StopMissionCommand command)
        {
            return m_LaunchService.StopAsync();
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;
        readonly StatusService m_StatusService;
        readonly MissionsService m_MissionsService;
        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfiguration;
        readonly LaunchService m_LaunchService;
    }
}
