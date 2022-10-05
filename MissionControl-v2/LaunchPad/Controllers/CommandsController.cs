using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.LaunchPad.Services;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Controllers
{
    [ApiController]
    [Route("api/v1/commands")]
    public class CommandsController : Controller
    {
        public CommandsController(CommandProcessor commandProcessor)
        {
            m_CommandProcessor = commandProcessor;
        }

        [HttpPost]
        public Task<IActionResult> Post(Command command)
        {
            return m_CommandProcessor.ProcessCommandAsync(command);
        }

        readonly CommandProcessor m_CommandProcessor;
    }
}
