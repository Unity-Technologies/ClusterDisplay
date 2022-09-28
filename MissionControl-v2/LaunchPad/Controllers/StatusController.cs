using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.LaunchPad.Services;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Controllers
{
    [ApiController]
    [Route("api/v1/status")]
    public class StatusController : Controller
    {
        public StatusController(IConfiguration configuration, StatusService statusService)
        {
            m_StatusService = statusService;
            m_Configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery]ulong minStatusNumber)
        {
            var futureStatus = m_StatusService.GetStatusAfter(minStatusNumber);
            if (futureStatus.IsCompleted)
            {
                return Ok(futureStatus.Result);
            }

            // New status not yet ready, setup to wait until it is ready or up to 2 minutes (to avoid blocking calls
            // to stay blocked for too long as some middleware (like some component of Azure WebApp) don't like http
            // calls that take too long to give a response...
            double maxSec = Convert.ToDouble(m_Configuration["blockingCallMaxSec"]);
            await Task.WhenAny(futureStatus, Task.Delay(TimeSpan.FromSeconds(maxSec)));

            if (futureStatus.IsCompleted)
            {
                return Ok(futureStatus.Result);
            }
            else
            {
                return NoContent();
            }
        }

        readonly IConfiguration m_Configuration;
        readonly StatusService m_StatusService;
    }
}
