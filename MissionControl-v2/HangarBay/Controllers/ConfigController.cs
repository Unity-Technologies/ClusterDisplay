using Microsoft.AspNetCore.Mvc;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Controllers
{
    [ApiController]
    [Route("api/v1/config")]
    public class ConfigController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}
