using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class StatusServiceExtension
    {
        public static void AddStatusService(this IServiceCollection services)
        {
            services.AddSingleton<StatusService>();
        }
    }

    public class StatusService: ObservableObjectService<Status>
    {
        public StatusService(IServiceProvider serviceProvider, ILogger<StatusService> logger)
            : base(serviceProvider, CreateNewStatus(logger), "status")
        {
        }

        protected override void OnObjectChanged(ObservableObject obj)
        {
            var newStatus = (Status)obj;
            if (newStatus.State != m_LastKnownState)
            {
                m_LastKnownState = newStatus.State;
                newStatus.EnteredStateTime = DateTime.Now;
            }
            base.OnObjectChanged(obj);
        }

        static Status CreateNewStatus(ILogger logger)
        {
            Status ret = new();
            ret.StartTime = DateTime.Now;
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion != null)
            {
                ret.Version = assemblyVersion.ToString();
            }
            else
            {
                ret.Version = "0.0.0.0";
                logger.LogError("Failed to get the assembly version, fall-back to {MissionControlVersion}",
                    ret.Version);
            }
            return ret;
        }

        State m_LastKnownState = State.Idle;
    }
}
