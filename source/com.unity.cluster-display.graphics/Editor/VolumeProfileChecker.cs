#if CLUSTER_DISPLAY_HDRP
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    // Performs sanity checks on Volume Profiles within the Assets directory.
    // Assumes the ones actually used are within this directory.
    class VolumeProfileChecker : AssetModificationProcessor
    {
        const string k_AssetsDirectory = "Assets";
        
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            var volumeProfiles = LoadAllAssetsOfType<VolumeProfile>(new[] { k_AssetsDirectory });
            foreach (var volumeProfile in volumeProfiles)
            {
                CheckVolumeProfile(volumeProfile);
            }
        }

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                if (path.Contains(k_AssetsDirectory) && Path.GetExtension(path) == ".asset")
                {
                    var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                    if (volumeProfile != null)
                    {
                        CheckVolumeProfile(volumeProfile);
                    }
                }
            }

            return paths;
        }

        static void CheckVolumeProfile(VolumeProfile volumeProfile)
        {
            foreach (var exposureComponent in volumeProfile.components.OfType<Exposure>())
            {
                if (!IsExposureFixed(exposureComponent.mode.value))
                {
                    var path = AssetDatabase.GetAssetPath(volumeProfile);
                    Debug.LogWarning($"Exposure component in volume profile \"{volumeProfile.name}\" " +
                        $"at {path} uses automatic mode. We recommend using fixed exposure with Cluster Display.");
                }
            }
        }

        // See IsExposureFixed in HDRenderPipeline.PostProcess.
        static bool IsExposureFixed(ExposureMode mode) => mode == ExposureMode.Fixed || mode == ExposureMode.UsePhysicalCamera;

        static T[] LoadAllAssetsOfType<T>(string[] searchInFolders) where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", searchInFolders)
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .ToArray();
        }
    }
}
#endif
