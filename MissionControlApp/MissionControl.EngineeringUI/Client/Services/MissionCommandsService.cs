using System.Net.Http.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionCommandsServiceExtension
    {
        public static void AddMissionCommandsService(this IServiceCollection services)
        {
            services.AddTransient<MissionCommandsService>();
        }
    }

    /// <summary>
    /// Service to execute mission commands.
    /// </summary>
    public class MissionCommandsService
    {
        public MissionCommandsService(HttpClient httpClient, IConfiguration configuration)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();
        }

        public Task LaunchMissionAsync()
        {
            return m_HttpClient.PostAsJsonAsync(k_Endpoint, new LaunchMissionCommand(), Json.SerializerOptions);
        }

        public Task StopCurrentMissionAsync()
        {
            return m_HttpClient.PostAsJsonAsync(k_Endpoint, new StopMissionCommand(), Json.SerializerOptions);
        }

        public Task SaveMissionAsync(SaveMissionCommand command)
        {
            return m_HttpClient.PostAsJsonAsync(k_Endpoint, command, Json.SerializerOptions);
        }

        public Task LoadMissionAsync(Guid missionId)
        {
            return m_HttpClient.PostAsJsonAsync(k_Endpoint, new LoadMissionCommand() {Identifier = missionId},
                Json.SerializerOptions);
        }

        readonly HttpClient m_HttpClient;

        const string k_Endpoint = "currentMission/commands";
    }
}
