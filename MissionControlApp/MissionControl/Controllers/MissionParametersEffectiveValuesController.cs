using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/parametersEffectiveValues")]
    public class MissionParametersEffectiveValuesController : ControllerBase
    {
        public MissionParametersEffectiveValuesController(
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService,
            MissionParametersEffectiveValuesService missionParametersEffectiveValuesService)
        {
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;
            m_MissionParametersEffectiveValuesService = missionParametersEffectiveValuesService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(m_MissionParametersEffectiveValuesService.CloneAll());
        }

        [Route("{Id}")]
        [HttpGet]
        public IActionResult Get(Guid id)
        {
            try
            {
                return Ok(m_MissionParametersEffectiveValuesService.CloneSingle(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Effective parameter value {id} cannot be found.");
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] MissionParameterValue value)
        {
            using var lockedLaunchConfiguration = await m_CurrentMissionLaunchConfigurationService.LockAsync();
            if (lockedLaunchConfiguration.Value.AssetId == Guid.Empty)
            {
                return Conflict($"Cannot set a effective mission parameter's value if there is no asset defined in " +
                    $"the current launch configuration.");
            }

            try
            {
                m_MissionParametersEffectiveValuesService.Set(value);
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
                    m_MissionParametersEffectiveValuesService.Delete(id);
                    return Ok();
                }
                catch (ArgumentException e)
                {
                    return BadRequest(e.ToString());
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Effective parameter value {id} cannot be found.");
            }
        }

        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
        readonly MissionParametersEffectiveValuesService m_MissionParametersEffectiveValuesService;
    }
}
