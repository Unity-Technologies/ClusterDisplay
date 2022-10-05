using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.LaunchPad.Services;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Controllers
{
    [ApiController]
    [Route("api/v1/health")]
    public class HealthController : ControllerBase
    {
        public HealthController(IHealthService healthService)
        {
            m_HealthService = healthService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_HealthService.Fetch());
        }

        readonly IHealthService m_HealthService;
    }
}
