using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/parameters")]
    public class MissionParametersController : ControllerBase
    {
        public MissionParametersController(
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService,
            MissionParametersService missionParametersService)
        {
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;
            m_MissionParametersService = missionParametersService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_MissionParametersService.CloneAll());
        }

        [Route("{Id}")]
        [HttpGet]
        public IActionResult Get(Guid id)
        {
            try
            {
                return Ok(m_MissionParametersService.CloneSingle(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Parameter {id} cannot be found.");
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] MissionParameter value)
        {
            using var lockedLaunchConfiguration = await m_CurrentMissionLaunchConfigurationService.LockAsync();
            if (lockedLaunchConfiguration.Value.AssetId == Guid.Empty)
            {
                return Conflict($"Cannot set a mission parameter if there is no asset defined in the current " +
                    $"launch configuration.");
            }

            try
            {
                m_MissionParametersService.Set(value);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }

        [Route("{Id}")]
        [HttpDelete]
        public IActionResult Delete(Guid id)
        {
            try
            {
                try
                {
                    m_MissionParametersService.Delete(id);
                    return Ok();
                }
                catch (ArgumentException e)
                {
                    return BadRequest(e.ToString());
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Parameter {id} cannot be found.");
            }
        }

        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
        readonly MissionParametersService m_MissionParametersService;
    }
}
