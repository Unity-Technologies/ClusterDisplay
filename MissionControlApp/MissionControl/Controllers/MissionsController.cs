using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/missions")]
    public class MissionsController : ControllerBase
    {
        public MissionsController(MissionsService missionsService)
        {
            m_MissionsService = missionsService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<SavedMissionSummary> retMissions = new();
            using (var lockedMissions = await m_MissionsService.Manager.GetLockedReadOnlyAsync())
            {
                foreach (var mission in lockedMissions.Value.Values)
                {
                    retMissions.Add(mission.DeepClone());
                }
            }
            return Ok(retMissions);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            SavedMissionSummary? retMission;
            using (var lockedAssets = await m_MissionsService.Manager.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retMission))
                {
                    return NotFound($"Saved mission summary {id} not found");
                }
                retMission = retMission.DeepClone();
            }
            return Ok(retMission);
        }

        [Route("{Id}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!await m_MissionsService.Manager.DeleteAsync(id))
            {
                return NotFound($"Cannot find saved mission {id}");
            }

            await m_MissionsService.SaveAsync();

            return Ok();
        }

        readonly MissionsService m_MissionsService;
    }
}
