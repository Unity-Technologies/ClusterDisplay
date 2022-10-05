using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/launchPadsHealth")]
    public class LaunchPadsHealthController : ControllerBase
    {
        public LaunchPadsHealthController(LaunchPadsHealthService launchPadsHealthService)
        {
            m_LaunchPadsHealthService = launchPadsHealthService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<LaunchPadHealth> retHealths = new();
            using (var lockedHealths = await m_LaunchPadsHealthService.GetLockedReadOnlyAsync())
            {
                foreach (var health in lockedHealths.Value.Values)
                {
                    retHealths.Add(health.DeepClone());
                }
            }
            return Ok(retHealths);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            LaunchPadHealth? retHealth;
            using (var lockedAssets = await m_LaunchPadsHealthService.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retHealth))
                {
                    return NotFound($"LaunchPad {id} status not found");
                }
                retHealth = retHealth.DeepClone();
            }
            return Ok(retHealth);
        }

        LaunchPadsHealthService m_LaunchPadsHealthService;
    }
}
