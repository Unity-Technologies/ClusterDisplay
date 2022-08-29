using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.HangarBay.Services;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Controllers
{
    [ApiController]
    [Route("api/v1/status")]
    public class StatusController : ControllerBase
    {
        public StatusController(StatusService statusService)
        {
            m_StatusService = statusService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_StatusService.Build());
        }

        private readonly StatusService m_StatusService;
    }
}
