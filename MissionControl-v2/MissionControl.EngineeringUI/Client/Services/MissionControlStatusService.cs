using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionControlStatusServiceExtension
    {
        public static void AddMissionControlStatusService(this IServiceCollection services)
        {
            services.AddSingleton<MissionControlStatusService>();
        }
    }

    /// <summary>
    /// Service making MissionControl's status available
    /// </summary>
    public class MissionControlStatusService: Status
    {
        public MissionControlStatusService(ObjectsUpdateService objectsUpdateService)
        {
            objectsUpdateService.RegisterForUpdates(ObservableObjectsName.Status, StatusUpdate);
        }

        void StatusUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<Status>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return;
            }
            DeepCopyFrom(deserializeRet);
            SignalChanges();
        }
    }
}
