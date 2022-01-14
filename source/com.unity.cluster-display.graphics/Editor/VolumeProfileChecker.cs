#if CLUSTER_DISPLAY_HDRP
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    class VolumeProfileChecker : AssetModificationProcessor
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            var volumeProfiles = Resources.FindObjectsOfTypeAll<VolumeProfile>();
            foreach (var volumeProfile in volumeProfiles)
            {
                CheckVolumeProfile(volumeProfile);
            }
        }

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                if (Path.GetExtension(path) == ".asset")
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
                    Debug.LogWarning($"Exposure component in volume profile \"{volumeProfile.name}\" " + 
                        "uses automatic mode. We recommend using fixed exposure with Cluster Display.");
                }
            }
        }

        // See IsExposureFixed in HDRenderPipeline.PostProcess.
        static bool IsExposureFixed(ExposureMode mode) => mode == ExposureMode.Fixed || mode == ExposureMode.UsePhysicalCamera;
    }
}
#endif
