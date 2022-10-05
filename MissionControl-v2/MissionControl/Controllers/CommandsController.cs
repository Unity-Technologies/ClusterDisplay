using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/commands")]
    public class CommandsController: ControllerBase
    {
        public CommandsController(CommandsService commandsService)
        {
            m_CommandsService = commandsService;
        }

        [HttpPost]
        public async Task<IActionResult> Post(Command command)
        {
            var (statusCode, errorMessage) = await m_CommandsService.ExecuteAsync(command);
            return StatusCode((int)statusCode, errorMessage);
        }

        readonly CommandsService m_CommandsService;
    }
}
