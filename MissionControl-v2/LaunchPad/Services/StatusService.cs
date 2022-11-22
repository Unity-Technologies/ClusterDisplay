using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Services
{
    public static class StatusServiceExtension
    {
        public static void AddStatusService(this IServiceCollection services)
        {
            services.AddSingleton<StatusService>();
        }
    }

    /// <summary>
    /// Service storing the current status of the LaunchPad.
    /// </summary>
    public class StatusService
    {
        public StatusService(ILogger<StatusService> logger, IHostApplicationLifetime applicationLifetime)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;

            m_Status.StartTime = DateTime.Now;
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion != null)
            {
                m_Status.Version = assemblyVersion.ToString();
            }
            else
            {
                m_Status.Version = "0.0.0.0";
                m_Logger.LogError("Failed to get the assembly version, fall-back to {LaunchPadVersion}",
                    m_Status.Version);
            }
            m_Status.LastChanged = m_Status.StartTime;

            m_ApplicationLifetime.ApplicationStopping.Register(ApplicationShutdown);
        }

        /// <summary>
        /// Current state of the LaunchPad.
        /// </summary>
        /// <remarks>Setter should only be called by CommandProcessor (he's the one driving the state of the LaunchPad).
        /// </remarks>
        public State State
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Status.State;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    if (value != m_Status.State)
                    {
                        m_Status.State = value;
                        StatusChanged();
                    }
                }
            }
        }

        /// <summary>
        /// Does the LaunchPad has to be restarted?
        /// </summary>
        public bool HasPendingRestart
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Status.PendingRestart;
                }
            }
        }

        /// <summary>
        /// Indicate that the LaunchPad that requires a restart.
        /// </summary>
        public void SignalPendingRestart()
        {
            lock (m_Lock)
            {
                if (!m_Status.PendingRestart)
                {
                    m_Status.PendingRestart = true;
                    StatusChanged();
                }
            }
        }

        /// <summary>
        /// Returns a copy of the current status.
        /// </summary>
        public Status GetCurrentStatus()
        {
            Status ret = new();
            lock (m_Lock)
            {
                ret.DeepCopyFrom(m_Status);
            }
            return ret;
        }

        /// <summary>
        /// Returns a task that will provide the status updated after the given version number.
        /// </summary>
        /// <param name="minStatusNumber">Minimum value of <see cref="Status.StatusNumber"/> to be returned (or wait
        /// until this value is reached).</param>
        public async Task<Status> GetStatusAfterAsync(ulong minStatusNumber)
        {
            Status ret = new();
            for (; ;)
            {
                Task somethingChangedTask;
                lock (m_Lock)
                {
                    if (m_Status.StatusNumber >= minStatusNumber)
                    {
                        ret.DeepCopyFrom(m_Status);
                        return ret;
                    }

                    m_StatusCompletionSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    somethingChangedTask = m_StatusCompletionSource.Task;
                }

                await somethingChangedTask;
            }
        }

        /// <summary>
        /// Method to be called when a set of changes to <see cref="m_Status"/> is completed.
        /// </summary>
        void StatusChanged()
        {
            m_Status.LastChanged = DateTime.Now;
            ++m_Status.StatusNumber;
            m_StatusCompletionSource?.SetResult();
            m_StatusCompletionSource = null;
        }

        /// <summary>
        /// Method called when the application is requested to shutdown.
        /// </summary>
        void ApplicationShutdown()
        {
            lock (m_Lock)
            {
                m_StatusCompletionSource?.TrySetCanceled();
            }
        }

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        readonly ILogger<StatusService> m_Logger;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        readonly IHostApplicationLifetime m_ApplicationLifetime;

        /// <summary>
        /// Synchronize access to member variables
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// Stores of the current status of the LaunchPad
        /// </summary>
        readonly Status m_Status = new();

        /// <summary>
        /// <see cref="Task"/> that gets completed every time m_Status is updated.
        /// </summary>
        TaskCompletionSource? m_StatusCompletionSource;
    }
}
