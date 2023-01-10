using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/currentMission/launchConfiguration")]
    public class CurrentMissionLaunchConfigurationController : ControllerBase
    {
        public CurrentMissionLaunchConfigurationController(
            CurrentMissionLaunchConfigurationService launchConfigurationService,
            StatusService statusService,
            AssetsService assetsService,
            ReferencedAssetsLockService referencedAssetsLockService)
        {
            m_LaunchConfigurationService = launchConfigurationService;
            m_StatusService = statusService;
            m_AssetsService = assetsService;
            m_ReferencedAssetsLockService = referencedAssetsLockService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            LaunchConfiguration retConfiguration;
            using (var lockedStatus = await m_LaunchConfigurationService.LockAsync())
            {
                retConfiguration = lockedStatus.Value.DeepClone();
            }
            return Ok(retConfiguration);
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] LaunchConfiguration configuration)
        {
            using (await m_ReferencedAssetsLockService.LockAsync())
            {
                if (configuration.AssetId != Guid.Empty)
                {
                    using var lockedAssets = await m_AssetsService.Manager.GetLockedReadOnlyAsync();
                    if (!lockedAssets.Value.ContainsKey(configuration.AssetId))
                    {
                        return BadRequest($"Asset {configuration.AssetId} cannot be found in the list of assets.");
                    }
                }

                using (var lockedStatus = await m_StatusService.LockAsync())
                {
                    if (lockedStatus.Value.State != State.Idle)
                    {
                        return Conflict($"Current mission's launch configuration can only be changed while idle (current " +
                            $"state is {lockedStatus.Value.State}).");
                    }

                    using (var lockedLaunchConfiguration = await m_LaunchConfigurationService.LockAsync())
                    {
                        if (!lockedLaunchConfiguration.Value.Equals(configuration))
                        {
                            lockedLaunchConfiguration.Value.DeepCopyFrom(configuration);
                            lockedLaunchConfiguration.Value.SignalChanges();
                        }
                    }
                }

                await m_LaunchConfigurationService.SaveAsync();

                return Ok();
            }
        }

        readonly CurrentMissionLaunchConfigurationService m_LaunchConfigurationService;
        readonly StatusService m_StatusService;
        readonly AssetsService m_AssetsService;
        readonly ReferencedAssetsLockService m_ReferencedAssetsLockService;
    }
}
