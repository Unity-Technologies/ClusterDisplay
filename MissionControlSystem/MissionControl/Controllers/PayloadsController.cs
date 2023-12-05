using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/payloads")]
    public class PayloadsController : ControllerBase
    {
        public PayloadsController(PayloadsService payloadsService)
        {
            m_PayloadsService = payloadsService;
        }

        [Route("{Id}")]
        [HttpGet]
        public IActionResult Get(Guid id)
        {
            try
            {
                return Ok(m_PayloadsService.Manager.GetPayload(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Cannot find a payload with the identifier {id}");
            }
        }

        readonly PayloadsService m_PayloadsService;
    }
}
