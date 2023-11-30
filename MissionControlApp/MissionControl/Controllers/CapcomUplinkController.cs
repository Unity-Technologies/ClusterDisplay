using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/capcomUplink")]
    public class CapcomUplinkController : ControllerBase
    {
        public CapcomUplinkController(CapcomUplinkService capcomUplinkService)
        {
            m_CapcomUplinkService = capcomUplinkService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            CapcomUplink retCapcomUplink = new();
            using (var lockedCapcomUplink = await m_CapcomUplinkService.LockAsync())
            {
                retCapcomUplink.DeepCopyFrom(lockedCapcomUplink.Value);
            }
            return Ok(retCapcomUplink);
        }

        readonly CapcomUplinkService m_CapcomUplinkService;
    }
}
