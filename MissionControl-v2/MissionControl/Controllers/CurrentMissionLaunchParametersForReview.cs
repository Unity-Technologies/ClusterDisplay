using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/launchParametersForReview")]
    public class CurrentMissionLaunchParametersForReview : ControllerBase
    {
        public CurrentMissionLaunchParametersForReview(LaunchService launchService)
        {
            m_LaunchService = launchService;
        }
        
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_LaunchService.GetLaunchParametersForReview());
        }

        [Route("{Id}")]
        [HttpGet]
        public IActionResult Get(Guid id)
        {
            try
            {
                return Ok(m_LaunchService.GetLaunchParameterForReview(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Cannot find a LaunchParameterForReview with the id {id}");
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] LaunchParameterForReview update)
        {
            try
            {
                m_LaunchService.UpdateLaunchParameterForReview(update);
                return Ok();
            }
            catch (KeyNotFoundException e)
            {
                return NotFound($"Cannot find LaunchParameterForReview with an id of {update.Id}: {e}");
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }

        LaunchService m_LaunchService;
    }
}
