using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;
using LaunchPadPrepareCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.PrepareCommand;
using LaunchPadAbortCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.AbortCommand;
using LaunchPadLaunchCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.LaunchCommand;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// The class responsible to "orchestrate" the launch.
    /// </summary>
    public class LaunchManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">The logger to which we send our messages.</param>
        /// <param name="httpClient"><see cref="HttpClient"/> to use to send REST requests.</param>
        public LaunchManager(ILogger logger, HttpClient httpClient)
        {
            m_Logger = logger;
            m_HttpClient = httpClient;
        }

        /// <summary>
        /// Structure to define the launch to be performed by <see cref="LaunchAsync"/>.
        /// </summary>
        public class LaunchManifest
        {
            /// <summary>
            /// The launch configuration.
            /// </summary>
            public LaunchConfiguration LaunchConfiguration { get; set; } = new LaunchConfiguration();

            /// <summary>
            /// The asset referenced by <see cref="LaunchConfiguration"/>.
            /// </summary>
            public Asset Asset { get; set; } = new Asset(Guid.Empty);

            /// <summary>
            /// The list of all available <see cref="LaunchComplex"/>es that can be referenced by
            /// <see cref="LaunchConfiguration"/>.
            /// </summary>
            public IEnumerable<LaunchComplex> Complexes { get; set; } = Enumerable.Empty<LaunchComplex>();

            /// <summary>
            /// General mission control configuration
            /// </summary>
            public Config Config { get; set; }

            /// <summary>
            /// Collection through which we observe status of launchpads change.
            /// </summary>
            /// <remarks>Only used through the different OnSomething callbacks so it is ok if we still use it even
            /// after this method call is over.</remarks>
            public IReadOnlyIncrementalCollection<LaunchPadStatus> LaunchPadsStatus { get; set; }
                = new IncrementalCollection<LaunchPadStatus>();
        }

        /// <summary>
        /// Initiate the launch.
        /// </summary>
        /// <param name="manifest">define the launch to be performed.</param>
        public async Task LaunchAsync(LaunchManifest manifest)
        {
            if (manifest.LaunchConfiguration.AssetId != manifest.Asset.Id)
            {
                throw new ArgumentException("launchConfiguration.AssetId != asset.Id");
            }

            lock (m_Lock)
            {
                if (m_LaunchPadsStatusValidation != null)
                {
                    throw new InvalidOperationException("There is already something launched.");
                }

                m_LaunchSignal = new();

                // Register to be informed of launchpad status changes.
                m_LaunchPadsStatusValidation = manifest.LaunchPadsStatus;
                m_LaunchPadsStatusValidation.OnObjectUpdated += LaunchPadStatusUpdate;
                m_LaunchPadsStatusValidation.OnObjectRemoved += LaunchPadStatusRemoved;

                // Start the work on each of the launchpads.
                foreach (var complexConfiguration in manifest.LaunchConfiguration.LaunchComplexes)
                {
                    // Find the LaunchComplex to use from the LaunchComplexes lists
                    var launchComplex = manifest.Complexes.FirstOrDefault(c => c.Id == complexConfiguration.Identifier);
                    if (launchComplex == null)
                    {
                        m_Logger.LogError("Cannot find LaunchComplex {Id} in the list of current LaunchComplexes",
                            complexConfiguration.Identifier);
                        m_LaunchPadsCount += complexConfiguration.LaunchPads.Count();
                        continue;
                    }

                    foreach (var launchpadConfiguration in complexConfiguration.LaunchPads)
                    {
                        ++m_LaunchPadsCount;

                        // Find the LaunchPad from the LaunchComplex
                        var launchPad = launchComplex.LaunchPads
                            .FirstOrDefault(lp => lp.Identifier == launchpadConfiguration.Identifier);
                        if (launchPad == null)
                        {
                            m_Logger.LogError("Cannot find LaunchPad {PadId} in the LaunchComplex {ComplexId}",
                                launchpadConfiguration.Identifier, complexConfiguration.Identifier);
                            continue;
                        }

                        // Get the last known status of the LaunchPad.
                        if (!manifest.LaunchPadsStatus.TryGetValue(launchPad.Identifier, out var lastKnownStatus))
                        {
                            m_Logger.LogError("Cannot find LaunchPad {PadId} status",
                                launchpadConfiguration.Identifier);
                            continue;
                        }

                        // Find the launchable to launch
                        var launchables = manifest.Asset.Launchables
                            .Where(l => l.Name == launchpadConfiguration.LaunchableName && launchPad.SuitableFor.Contains(l.Type));
                        if (launchables.Count() > 1)
                        {
                            m_Logger.LogError("Multiple launchables found with the name {Name} for LaunchPad {PadId}",
                                launchpadConfiguration.LaunchableName, launchpadConfiguration.Identifier);
                            continue;
                        }
                        Launchable? launchable = launchables.FirstOrDefault();
                        if (launchable == null)
                        {
                            m_Logger.LogError("Cannot find any launchable to launch on {PadId}",
                                launchpadConfiguration.Identifier);
                            continue;
                        }

                        // Start the launchpad supervisor that will take care of it.
                        LaunchPadSupervisor supervisor = new (m_Logger, m_HttpClient, manifest.Config, launchPad,
                            launchpadConfiguration, launchable, lastKnownStatus.State, m_LaunchSignal.Task);
                        _ = supervisor.LaunchAsync();
                        m_Supervisors.Add(launchPad.Identifier, supervisor);
                    }
                }

                if (m_Supervisors.Count == 0)
                {
                    // Complete launch failure, not even a launchpad was able to even try to start something...
                    return;
                }

                SetRunningLaunchPads(m_Supervisors.Count());
            }

            await ContinueLaunchAsync();
        }

        /// <summary>
        /// Stop whatever is currently running
        /// </summary>
        public void Stop()
        {
            lock (m_Lock)
            {
                foreach (var supervisor in m_Supervisors.Values)
                {
                    supervisor.Stop();
                }
            }
        }

        /// <summary>
        /// Was a <see cref="LaunchAsync"/> call made but no <see cref="Conclude"/>?
        /// </summary>
        public bool NeedsConcludeCall
        {
            get
            {
                lock (m_Lock)
                {
                    return m_LaunchPadsStatusValidation != null;
                }
            }
        }

        /// <summary>
        /// Number of launchpads that are part of the current launch (0 if nothing is launched).
        /// </summary>
        public int LaunchPadsCount
        {
            get
            {
                lock (m_Lock)
                {
                    return m_LaunchPadsCount;
                }
            }
        }

        /// <summary>
        /// How many launchpads are still running (can be getting payload, running the pre-launch sequence, launched,
        /// ...) as long as it is still doing something for the mission.
        /// </summary>
        public int RunningLaunchPads
        {
            get
            {
                lock (m_Lock)
                {
                    return m_RunningLaunchPads;
                }
            }
        }

        /// <summary>
        /// Returns a task that completes when <see cref="RunningLaunchPads"/> changes.
        /// </summary>
        public Task RunningLaunchPadsChanged
        {
            get
            {
                lock (m_Lock)
                {
                    m_RunningLaunchPadsChanged ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    return m_RunningLaunchPadsChanged.Task;
                }
            }
        }

        /// <summary>
        /// Task indicating that we are done preparing the launchpads and they have received the signal to launch their
        /// payload.
        /// </summary>
        public Task Launched
        {
            get
            {
                lock (m_Lock)
                {
                    return m_LaunchSignal?.Task ?? Task.CompletedTask;
                }
            }
        }

        /// <summary>
        /// Method to be called once the task returned by <see cref="LaunchAsync"/>.
        /// </summary>
        /// <param name="launchPadsStatus">Same collection of <see cref="LaunchPadStatus"/> that was passed to
        /// <see cref="LaunchAsync"/>.</param>
        public void Conclude(IReadOnlyIncrementalCollection<LaunchPadStatus> launchPadsStatus)
        {
            lock (m_Lock)
            {
                if (m_Supervisors.Count > 0)
                {
                    throw new InvalidOperationException("Looks like the task returned by LaunchAsync is not over yet, " +
                        "call Stop before calling conclude.");
                }

                if (m_LaunchPadsStatusValidation != null)
                {
                    Debug.Assert(launchPadsStatus == m_LaunchPadsStatusValidation);
                    launchPadsStatus.OnObjectUpdated -= LaunchPadStatusUpdate;
                    launchPadsStatus.OnObjectRemoved -= LaunchPadStatusRemoved;
                    m_LaunchPadsStatusValidation = null;
                }

                if (m_LaunchSignal != null)
                {
                    m_LaunchSignal.TrySetResult();
                    m_LaunchSignal = null;
                }

                m_LaunchPadsCount = 0;
                m_RunningLaunchPads = 0;
                if (m_RunningLaunchPadsChanged != null)
                {
                    m_RunningLaunchPadsChanged.TrySetResult();
                    m_RunningLaunchPadsChanged = null;
                }
            }
        }

        /// <summary>
        /// Continue monitoring launch in the background (until it is stopped).
        /// </summary>
        async Task ContinueLaunchAsync()
        {
            try
            {
                // Wait until launchpads are ready to launch (or fail)
                List<Task> readyForLaunchTasks;
                lock (m_Lock)
                {
                    readyForLaunchTasks = m_Supervisors.Values.Select(s => s.WaitingForLaunchTask).ToList();
                }
                await Task.WhenAll(readyForLaunchTasks);

                lock (m_Lock)
                {
                    // Clear launchpads in error
                    foreach (var supervisor in m_Supervisors.ToList())
                    {
                        if (supervisor.Value.IsDone)
                        {
                            m_Supervisors.Remove(supervisor.Key);
                        }
                    }
                    SetRunningLaunchPads(m_Supervisors.Count);

                    if (m_Supervisors.Count == 0)
                    {
                        m_Logger.LogError("No launchpad succeeded in preparing for launch, launch failed");
                        m_LaunchSignal?.SetResult();
                        return;
                    }
                }

                // Signal to the supervisors it is the time to launch!
                m_LaunchSignal?.SetResult();

                // Wait for every launchpads to be done
                List<Task> doneTasks = new();
                lock (m_Lock)
                {
                    foreach (var supervisor in m_Supervisors.Values)
                    {
                        doneTasks.Add(supervisor.DoneTask.ContinueWith(_ =>
                        {
                            lock (m_Lock)
                            {
                                SetRunningLaunchPads(m_RunningLaunchPads - 1);
                            }
                        }));
                    }
                }
                await Task.WhenAll(doneTasks);
            }
            finally
            {
                lock (m_Lock)
                {
                    m_Supervisors.Clear();
                }
            }
        }

        /// <summary>
        /// Delegate called when MissionControl receives notification about the state change of a
        /// <see cref="LaunchPad"/>.
        /// </summary>
        /// <param name="newStatus">New <see cref="LaunchPad"/> status.</param>
        void LaunchPadStatusUpdate(LaunchPadStatus newStatus)
        {
            lock (m_Lock)
            {
                if (m_Supervisors.TryGetValue(newStatus.Id, out var supervisor))
                {
                    supervisor.StatusChanged(newStatus);
                }
            }
        }

        /// <summary>
        /// Delegate called when MissionControl stops monitoring status of a <see cref="LaunchPad"/>.
        /// </summary>
        /// <remarks>This only happens as a consequence of <see cref="LaunchComplex"/>es being modified which should not
        /// happen when we are launching something (or when something is launched).</remarks>
        static void LaunchPadStatusRemoved(LaunchPadStatus _)
        {
            Debug.Assert(false, "LaunchPadStatus was removed from the collection while managing a launch, this is not " +
                "supposed to happen, launch complexes should be locked while mission control state is not idle.");
        }

        /// <summary>
        /// Sets the number of running launchpads.
        /// </summary>
        /// <param name="value">New value</param>
        /// <remarks>Caller should have locked <see cref="m_Lock"/> before calling this method.</remarks>
        void SetRunningLaunchPads(int value)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));

            if (value == m_RunningLaunchPads)
            {
                return;
            }

            m_RunningLaunchPads = value;
            if (m_RunningLaunchPadsChanged != null)
            {
                m_RunningLaunchPadsChanged.TrySetResult();
                m_RunningLaunchPadsChanged = null;
            }
        }

        /// <summary>
        /// Class for objects responsible for managing the launch process for a given <see cref="LaunchPad"/>.
        /// </summary>
        class LaunchPadSupervisor
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="logger">The logger to which we send our messages.</param>
            /// <param name="httpClient"><see cref="HttpClient"/> to use to send REST requests.</param>
            /// <param name="config">MissionControl's configuration.</param>
            /// <param name="definition">Launchpad's definition (does not depend on the mission).</param>
            /// <param name="configuration">Launchpad's configuration for that mission.</param>
            /// <param name="launchable">To launch.</param>
            /// <param name="initialState">Initial state of the <see cref="LaunchPad"/>.</param>
            /// <param name="launchSignal">Task indicating to the supervisor it is the time to send the
            /// <see cref="LaunchPadLaunchCommand"/> to the <see cref="LaunchPad"/>.</param>
            public LaunchPadSupervisor(ILogger logger, HttpClient httpClient, Config config, LaunchPad definition,
                LaunchPadConfiguration configuration, Launchable launchable, LaunchPadState initialState,
                Task launchSignal)
            {
                m_Logger = logger;
                m_HttpClient = httpClient;
                m_Config = config;
                m_Definition = definition;
                m_Configuration = configuration;
                m_Launchable = launchable;
                m_CurrentState = initialState;
                m_LaunchSignal = launchSignal;

                if (m_CurrentState == LaunchPadState.Idle)
                {
                    m_Step0Idle.TrySetResult();
                }
            }

            /// <summary>
            /// Perform all the steps to launch the <see cref="LaunchPad"/> (and monitor it once launched).
            /// </summary>
            public async Task LaunchAsync()
            {
                try
                {
                    await LaunchAsyncImplementation();
                }
                catch (Exception e)
                {
                    m_Logger.LogError(e, "{PadId} failed launch", m_Definition.Identifier);
                    lock (m_Lock)
                    {
                        Done();
                    }
                }
            }

            /// <summary>
            /// Inform the supervisor of a change in the status of the <see cref="LaunchPad"/>.
            /// </summary>
            /// <param name="status">New status</param>
            public void StatusChanged(LaunchPadStatus status)
            {
                lock (m_Lock)
                {
                    if (m_IsDone)
                    {
                        // We are done, no need to do anything of the state
                        return;
                    }

                    // This could in theory happen if there is a connection glitch, so let's be forgiving and only fail
                    // if we are more than 30 seconds waiting for feedback from the launchpad.
                    if (!status.IsDefined)
                    {
                        if (!m_UndefinedTime.IsRunning)
                        {
                            m_UndefinedTime.Reset();
                            m_UndefinedTime.Start();
                            Task.Delay(TimeSpan.FromSeconds(m_Config.LaunchPadFeedbackTimeoutSec + 1))
                                .ContinueWith(_ => {
                                    lock (m_Lock)
                                    {
                                        if (m_UndefinedTime.IsRunning &&
                                            m_UndefinedTime.Elapsed > TimeSpan.FromSeconds(m_Config.LaunchPadFeedbackTimeoutSec))
                                        {
                                            m_UndefinedTime.Stop();
                                            m_Logger.LogError("LaunchPad {PadId} state is undefined for too long " +
                                                "{Elapsed}, looks like some networking problem or a failure of the " +
                                                "LaunchPad", m_Definition.Identifier, m_UndefinedTime.Elapsed);
                                            Done();
                                        }
                                    }
                                });
                        }
                        return;
                    }
                    m_UndefinedTime.Stop();

                    // Quickly skip cases where something else than state changed in the status
                    if (status.State == m_CurrentState)
                    {
                        return;
                    }

                    // Try to detect someone else messing up with "our" launchpad (through unexpected state changes)!
                    if ((m_Step0Idle.Task.IsCompleted || status.State != LaunchPadState.Idle) &&
                        (int)status.State < (int)m_CurrentState)
                    {
                        if (!m_AbortPosted)
                        {
                            m_Logger.LogError("LaunchPad {PadId} state has moved backward, from {From} to {To}, " +
                                "could the LaunchPad be used by another MissionControl?", m_Definition.Identifier,
                                m_CurrentState, status.State);
                        }
                        Done();
                        return;
                    }

                    switch (status.State)
                    {
                        case LaunchPadState.Idle:
                            if (!m_AbortPosted)
                            {
                                m_Step0Idle.TrySetResult();
                            }
                            else
                            {
                                Done();
                            }
                            break;
                        case LaunchPadState.GettingPayload:
                        case LaunchPadState.PreLaunch:
                            // Normal steps while preparing, nothing to do
                            break;
                        case LaunchPadState.WaitingForLaunch:
                            m_Step1WaitingForLaunch.TrySetResult();
                            break;
                        case LaunchPadState.Launched:
                            m_Step2Launched.TrySetResult();
                            break;
                        case LaunchPadState.Over:
                            Done();
                            break;
                    }
                    m_CurrentState = status.State;
                }
            }

            /// <summary>
            /// Task that get signaled when the <see cref="LaunchPad"/> is ready for launch (or fail to launch).
            /// </summary>
            public Task WaitingForLaunchTask => m_Step1WaitingForLaunch.Task;

            /// <summary>
            /// Task that get signaled when the <see cref="LaunchPad"/> work is over (and its state is
            /// <see cref="LaunchPadState.Over"/>).
            /// </summary>
            public Task DoneTask => m_Step3Done.Task;

            /// <summary>
            /// Returns if the supervisor & launchpad are done doing their work.
            /// </summary>
            public bool IsDone
            {
                get
                {
                    lock (m_Lock)
                    {
                        return m_IsDone;
                    }
                }
            }

            /// <summary>
            /// Stop whatever is running
            /// </summary>
            public void Stop()
            {
                lock (m_Lock)
                {
                    if (m_IsDone)
                    {   // Already done, nothing to do.
                        return;
                    }

                    // Send an abort command
                    // Fire and forget, nothing to report in case of a failure (which shouldn't happen anyway)
                    _ = PostCommand(new LaunchPadAbortCommand());

                    // Remember we posted an abort (to avoid unnecessary error messages in state processing).
                    m_AbortPosted = true;
                }
            }

            /// <summary>
            /// Perform all the steps to launch the <see cref="LaunchPad"/> (and monitor it once launched).
            /// </summary>
            async Task LaunchAsyncImplementation()
            {
                // Be sure the launchpad is idle before trying to do anything
                Task<HttpResponseMessage>? postTask = null;
                lock (m_Lock)
                {
                    if (m_CurrentState != LaunchPadState.Idle)
                    {
                        postTask = PostCommand(new LaunchPadAbortCommand());
                    }
                }
                if (postTask != null)
                {
                    (await postTask).EnsureSuccessStatusCode();
                }

                // We always want to start from idle
                await m_Step0Idle.Task;
                lock (m_Lock)
                {
                    if (m_IsDone)
                    {
                        return;
                    }
                    if (m_CurrentState != LaunchPadState.Idle)
                    {
                        m_Logger.LogError("LaunchPad {PadId} expected to be Idle but is {State}",
                            m_Definition.Identifier, m_CurrentState);
                        Done();
                    }
                }

                // Prepare the launchpad
                LaunchPadPrepareCommand prepareCommand = new();
                prepareCommand.PayloadIds = m_Launchable.Payloads;
                prepareCommand.PayloadSource = m_Config.LaunchPadsEntry;
                prepareCommand.LaunchableData = m_Launchable.Data;
                //prepareCommand.LaunchData // TODO Set from parameters
                prepareCommand.PreLaunchPath = m_Launchable.PreLaunchPath;
                prepareCommand.LaunchPath = m_Launchable.LaunchPath;

                var commandRet = await PostCommand(prepareCommand);
                commandRet.EnsureSuccessStatusCode();

                // Wait it is ready for launch
                await m_Step1WaitingForLaunch.Task;
                lock (m_Lock)
                {
                    if (m_IsDone)
                    {
                        return;
                    }
                    if (m_CurrentState != LaunchPadState.WaitingForLaunch)
                    {
                        m_Logger.LogError("LaunchPad {PadId} expected to be WaitingForLaunch but is {State}",
                            m_Definition.Identifier, m_CurrentState);
                        Done();
                    }
                }

                // Wait for the launch signal
                await m_LaunchSignal;

                // Send the LaunchCommand
                LaunchPadLaunchCommand launchCommand = new();
                commandRet = await PostCommand(launchCommand);
                commandRet.EnsureSuccessStatusCode();

                // Wait for the LaunchPad to be launched
                await m_Step2Launched.Task;

                // Well, nothing special to do when launch, just check everything is ok.
                // Maybe some more work will be needed in the future.
                lock (m_Lock)
                {
                    if (m_IsDone)
                    {
                        return;
                    }
                }

                // Wait for everything to be done
                await m_Step3Done.Task;

                // Again, nothing special to do for now, we are done!
            }

            /// <summary>
            /// Execution for that supervisor and launchpad is done (either normal end or failure).
            /// </summary>
            /// <remarks>Assumes the caller has locked <see cref="m_Lock"/>.</remarks>
            void Done()
            {
                Debug.Assert(Monitor.IsEntered(m_Lock));

                m_IsDone = true;

                // Signal everything to unblock Launch method.
                m_Step0Idle.TrySetResult();
                m_Step1WaitingForLaunch.TrySetResult();
                m_Step2Launched.TrySetResult();
                m_Step3Done.TrySetResult();
            }

            /// <summary>
            /// Post a command to the <see cref="LaunchPad"/>.
            /// </summary>
            /// <typeparam name="T">The command to post.</typeparam>
            Task<HttpResponseMessage> PostCommand<T>(T command) where T: ClusterDisplay.MissionControl.LaunchPad.Command
            {
                Uri postUri = new(m_Definition.Endpoint, "api/v1/commands");
                return m_HttpClient.PostAsJsonAsync(postUri, command, Json.SerializerOptions);
            }

            readonly ILogger m_Logger;
            readonly HttpClient m_HttpClient;

            /// <summary>
            /// MissionControl's configuration.
            /// </summary>
            readonly Config m_Config;

            /// <summary>
            /// Launchpad's definition (does not depend on the mission)
            /// </summary>
            readonly LaunchPad m_Definition;

            /// <summary>
            /// Launchpad's configuration for that mission.
            /// </summary>
            // ReSharper disable once NotAccessedField.Local -> Not currently used but will be whe we add support for
            // launch parameters.
            readonly LaunchPadConfiguration m_Configuration;

            /// <summary>
            /// To launch.
            /// </summary>
            readonly Launchable m_Launchable;

            /// <summary>
            /// Task indicating to the supervisor it is the time to send the <see cref="LaunchPadLaunchCommand"/> to
            /// the <see cref="LaunchPad"/>.
            /// </summary>
            readonly Task m_LaunchSignal;

            /// <summary>
            /// Task marked as completed when the <see cref="LaunchPad"/> is idle and ready to start preparing the
            /// launch.
            /// </summary>
            readonly TaskCompletionSource m_Step0Idle = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Task marked as completed when the <see cref="LaunchPad"/> is ready to launch.
            /// </summary>
            readonly TaskCompletionSource m_Step1WaitingForLaunch = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Task marked as completed when the <see cref="LaunchPad"/> is launched.
            /// </summary>
            readonly TaskCompletionSource m_Step2Launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Task marked as completed when the <see cref="LaunchPad"/>'s mission is over.
            /// </summary>
            readonly TaskCompletionSource m_Step3Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Lock used to synchronize access to member variables below
            /// </summary>
            readonly object m_Lock = new();

            /// <summary>
            /// <see cref="LaunchPad"/>'s current state.
            /// </summary>
            LaunchPadState m_CurrentState;

            /// <summary>
            /// Are we done?
            /// </summary>
            bool m_IsDone;

            /// <summary>
            /// Have we posted an abort message?
            /// </summary>
            bool m_AbortPosted;

            /// <summary>
            /// For how long is the state of the associated LaunchPad undefined?
            /// </summary>
            Stopwatch m_UndefinedTime = new();
        }

        readonly ILogger m_Logger;
        readonly HttpClient m_HttpClient;

        /// <summary>
        /// Synchronize access to the member variables below.
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// To signal it is time to launch every launchpads.
        /// </summary>
        TaskCompletionSource? m_LaunchSignal;

        /// <summary>
        /// Number of launchpads that are part of the current launch (0 if nothing is launched).
        /// </summary>
        int m_LaunchPadsCount;

        /// <summary>
        /// How many launchpads are still running (can be getting payload, running the pre-launch sequence, launched,
        /// ...) as long as it is still doing something for the mission.
        /// </summary>
        int m_RunningLaunchPads;

        /// <summary>
        /// Tasks that gets completed when m_RunningLaunchPads changes.
        /// </summary>
        TaskCompletionSource? m_RunningLaunchPadsChanged;

        /// <summary>
        /// Variable to be used only as part of the <see cref="Conclude"/> method to validate we are called with the
        /// same <see cref="IReadOnlyIncrementalCollection{LaunchPadStatus}"/> than at launch.
        /// </summary>
        /// <remarks>Using it for anything else is asking for trouble as we do not have a lock on it and so might face
        /// all sort of race conditions.</remarks>
        IReadOnlyIncrementalCollection<LaunchPadStatus>? m_LaunchPadsStatusValidation;

        /// <summary>
        /// Objects supervising each of the <see cref="LaunchPad"/>s.
        /// </summary>
        readonly Dictionary<Guid, LaunchPadSupervisor> m_Supervisors = new();
    }
}
