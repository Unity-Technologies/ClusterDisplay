using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Unity.ClusterDisplay.MissionControl.HangarBay.Services;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Controllers
{
    [ApiController]
    [Route("api/v1/commands")]
    public class CommandsController: ControllerBase
    {
        public CommandsController(ILogger<CommandsController> logger, IHostApplicationLifetime applicationLifetime)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;
        }

        [HttpPost]
        public IActionResult Post(Command command)
        {
            return command.Type switch
            {
                CommandType.Prepare => OnPrepare((PrepareCommand)command),
                CommandType.Shutdown => OnSutdown((ShutdownCommand)command),
                _ => BadRequest()
            };
        }

        public IActionResult OnPrepare(PrepareCommand command)
        {
            return Ok();
        }

        public IActionResult OnSutdown(ShutdownCommand command)
        {
            m_ApplicationLifetime.StopApplication();
            return Accepted();
        }

        readonly ILogger<CommandsController> m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;
    }
}
