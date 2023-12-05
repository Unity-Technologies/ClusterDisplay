using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/parametersDesiredValues")]
    public class MissionParametersDesiredValuesController : ControllerBase
    {
        public MissionParametersDesiredValuesController(
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService,
            MissionParametersDesiredValuesService missionParametersDesiredValuesService)
        {
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;
            m_MissionParametersDesiredValuesService = missionParametersDesiredValuesService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_MissionParametersDesiredValuesService.CloneAll());
        }

        [Route("{Id}")]
        [HttpGet]
        public IActionResult Get(Guid id)
        {
            try
            {
                return Ok(m_MissionParametersDesiredValuesService.CloneSingle(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Desired parameter value {id} cannot be found.");
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] MissionParameterValue value)
        {
            using var lockedLaunchConfiguration = await m_CurrentMissionLaunchConfigurationService.LockAsync();
            if (lockedLaunchConfiguration.Value.AssetId == Guid.Empty)
            {
                return Conflict($"Cannot set a desired mission parameter's value if there is no asset defined in " +
                    $"the current launch configuration.");
            }

            try
            {
                m_MissionParametersDesiredValuesService.Set(value);
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
                    m_MissionParametersDesiredValuesService.Delete(id);
                    return Ok();
                }
                catch (ArgumentException e)
                {
                    return BadRequest(e.ToString());
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Desired parameter value {id} cannot be found.");
            }
        }

        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
        readonly MissionParametersDesiredValuesService m_MissionParametersDesiredValuesService;
    }
}
