using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
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
        public async Task<IActionResult> Get()
        {
            Status retStatus = new();
            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                retStatus.DeepCopyFrom(lockedStatus.Value);
            }
            return Ok(retStatus);
        }

        readonly StatusService m_StatusService;
    }
}
