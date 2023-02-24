using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/assets")]
    public class AssetsController : ControllerBase
    {
        public AssetsController(AssetsService assetsService,
            ReferencedAssetsLockService referencedAssetsLockService,
            StatusService statusService,
            FileBlobsService fileBlobsService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService,
            MissionsService missionsService)
        {
            m_AssetsService = assetsService;
            m_ReferencedAssetsLockService = referencedAssetsLockService;
            m_StatusService = statusService;
            m_FileBlobsService = fileBlobsService;
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;
            m_MissionsService = missionsService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<Asset> retAssets = new();
            using (var lockedAssets = await m_AssetsService.Manager.GetLockedReadOnlyAsync())
            {
                foreach (var asset in lockedAssets.Value.Values)
                {
                    retAssets.Add(asset.DeepClone());
                }
            }
            return Ok(retAssets);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            Asset? retAsset;
            using (var lockedAssets = await m_AssetsService.Manager.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retAsset))
                {
                    return NotFound($"Asset {id} not found");
                }
                retAsset = retAsset.DeepClone();
            }
            return Ok(retAsset);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] AssetPost asset)
        {
            if (string.IsNullOrEmpty(asset.Name))
            {
                return BadRequest("New asset need to have a name");
            }
            if (!Directory.Exists(asset.Url))
            {
                return NotFound($"Cannot access {asset.Url}");
            }

            try
            {
                // Remarks: For now we only support accessing through files, we might need to modify this part of the
                // code in the future (to analyze the url) and create a IAssetSource that depends on the url.
                FileAssetSource assetSource = new(asset.Url);

                Guid assetId = await m_AssetsService.Manager.AddAssetAsync(asset, assetSource);

                using (var lockedStatus = await m_StatusService.LockAsync())
                {
                    lockedStatus.Value.StorageFolders = m_FileBlobsService.Manager.GetStorageFolderStatus();
                    lockedStatus.Value.SignalChanges();
                }

                await m_AssetsService.SaveAsync();

                return Ok(assetId);
            }
            catch (IOException e)
            {
                return NotFound(e.ToString());
            }
            catch (CatalogException e)
            {
                return BadRequest($"LaunchCatalog.json appear to have an error.  Most likely causes are:\n" +
                    $"- Asset modified after it was produced.\n" +
                    $"- Mission Control integration not activated when producing the asset." +
                    $"\n\nError details: {e}");
            }
            catch (StorageFolderFullException e)
            {
                return StatusCode(StatusCodes.Status507InsufficientStorage, e.ToString());
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }

        [Route("{Id}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            using (await m_ReferencedAssetsLockService.LockAsync())
            {
                using (var lockedLaunchConfiguration = await m_CurrentMissionLaunchConfigurationService.LockAsync())
                {
                    if (lockedLaunchConfiguration.Value.AssetId == id)
                    {
                        return Conflict($"{id} is being used by currentMission/launchConfiguration");
                    }
                }

                if (await m_MissionsService.Manager.IsAssetInUseAsync(id))
                {
                    return Conflict($"{id} is being used by a saved mission");
                }

                if (!await m_AssetsService.Manager.RemoveAssetAsync(id))
                {
                    return NotFound($"No asset with the identifier of {id} can be found");
                }

                using (var lockedStatus = await m_StatusService.LockAsync())
                {
                    lockedStatus.Value.StorageFolders = m_FileBlobsService.Manager.GetStorageFolderStatus();
                    lockedStatus.Value.SignalChanges();
                }
            }

            await m_AssetsService.SaveAsync();

            return Ok();
        }

        readonly AssetsService m_AssetsService;
        readonly ReferencedAssetsLockService m_ReferencedAssetsLockService;
        readonly StatusService m_StatusService;
        readonly FileBlobsService m_FileBlobsService;
        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
        readonly MissionsService m_MissionsService;
    }
}
