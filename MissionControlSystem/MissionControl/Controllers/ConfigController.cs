using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/config")]
    public class ConfigController : ControllerBase
    {
        public ConfigController(ConfigService configService, StatusService statusService)
        {
            m_Config = configService;
            m_StatusService = statusService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_Config.Current);
        }

        [HttpPut]
        public async Task<IActionResult> Put(Config config)
        {
            using var lockedStatus = await m_StatusService.LockAsync();
            if (lockedStatus.Value.State != State.Idle)
            {
                // Remarks, in theory I guess we could allow changes while not idle, however we might be missing some
                // border effects that are not easy to think of and the need to support it is so far not obvious.  So
                // let's play safe and simply refuse it.
                return Conflict($"MissionControl has to be idle state to modify its configuration (it is " +
                        $"currently {lockedStatus.Value.State}).");
            }

            var ret = await m_Config.SetCurrentAsync(config, lockedStatus);
            if (ret.Any())
            {
                return BadRequest(ret);
            }
            if (lockedStatus.Value.PendingRestart)
            {
                return Accepted();
            }
            else
            {
                return Ok();
            }
        }

        readonly ConfigService m_Config;
        readonly StatusService m_StatusService;
    }
}
