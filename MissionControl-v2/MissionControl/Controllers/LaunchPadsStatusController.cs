using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/launchPadsStatus")]
    public class LaunchPadsStatusController : ControllerBase
    {
        public LaunchPadsStatusController(LaunchPadsStatusService launchPadsStatusService)
        {
            m_LaunchPadsStatusService = launchPadsStatusService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<LaunchPadStatus> retStatuses = new();
            using (var lockedStatuses = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                foreach (var status in lockedStatuses.Value.Values)
                {
                    retStatuses.Add(status.DeepClone());
                }
            }
            return Ok(retStatuses);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            LaunchPadStatus? retStatus;
            using (var lockedAssets = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retStatus))
                {
                    return NotFound($"LaunchPad {id} status not found");
                }
                retStatus = retStatus.DeepClone();
            }
            return Ok(retStatus);
        }

        LaunchPadsStatusService m_LaunchPadsStatusService;
    }
}
