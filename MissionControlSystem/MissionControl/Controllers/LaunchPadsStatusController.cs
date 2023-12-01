using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/launchPadsStatus")]
    public class LaunchPadsStatusController : ControllerBase
    {
        public LaunchPadsStatusController(LaunchPadsStatusService launchPadsStatusService, StatusService statusService)
        {
            m_LaunchPadsStatusService = launchPadsStatusService;
            m_StatusService = statusService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<LaunchPadStatus> retStatuses = new();
            using (var lockedStatuses = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                foreach (var status in lockedStatuses.Value.Values)
                {
                    retStatuses.Add(status.DeepClone());
                }
            }
            return Ok(retStatuses);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            LaunchPadStatus? retStatus;
            using (var lockedAssets = await m_LaunchPadsStatusService.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retStatus))
                {
                    return NotFound($"LaunchPad {id} status not found");
                }
                retStatus = retStatus.DeepClone();
            }
            return Ok(retStatus);
        }

        [Route("{Id}/dynamicEntries")]
        [HttpPut]
        public async Task<IActionResult> PutDynamicEntry(Guid id, [FromBody] JsonElement putBody)
        {
            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                if (lockedStatus.Value.State < State.Launched)
                {
                    return Conflict($"MissionControl has to be in the Launched state (or launched with failures) to " +
                        $"put dynamic launchpad status entries (it is currently {lockedStatus.Value.State}).");
                }

                try
                {
                    IEnumerable<LaunchPadReportDynamicEntry> dynamicEntries;
                    if (putBody.ValueKind == JsonValueKind.Array)
                    {
                        dynamicEntries = putBody.Deserialize<LaunchPadReportDynamicEntry[]>(Json.SerializerOptions)!;
                    }
                    else
                    {
                        dynamicEntries = Enumerable.Empty<LaunchPadReportDynamicEntry>().Append(
                            putBody.Deserialize<LaunchPadReportDynamicEntry>(Json.SerializerOptions)!);
                    }
                    foreach (var dynamicEntry in dynamicEntries)
                    {
                        m_LaunchPadsStatusService.PutDynamicEntry(id, dynamicEntry);
                    }
                }
                catch (KeyNotFoundException)
                {
                    return NotFound($"Launchpad {id} not found.");
                }
                catch (Exception e)
                {
                    return BadRequest(e.ToString());
                }
            }

            return Ok();
        }

        readonly LaunchPadsStatusService m_LaunchPadsStatusService;
        readonly StatusService m_StatusService;
    }
}
