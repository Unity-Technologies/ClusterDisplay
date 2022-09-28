using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.LaunchPad.Services;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Controllers
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
            var ret = await m_Config.SetCurrent(config);
            if (ret.Any())
            {
                return BadRequest(ret);
            }
            if (m_StatusService.HasPendingRestart)
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
