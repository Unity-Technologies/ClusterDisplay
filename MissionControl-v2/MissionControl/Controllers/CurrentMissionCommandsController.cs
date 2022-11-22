using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/commands")]
    public class CurrentMissionCommandsController : ControllerBase
    {
        public CurrentMissionCommandsController(MissionCommandsService missionCommandsService)
        {
            m_MissionCommandsService = missionCommandsService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] MissionCommand command)
        {
            var (statusCode, errorMessage) = await m_MissionCommandsService.ExecuteAsync(command);
            return StatusCode((int)statusCode, errorMessage);
        }

        readonly MissionCommandsService m_MissionCommandsService;
    }
}
