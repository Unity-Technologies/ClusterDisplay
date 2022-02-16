using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.EditorTests
{
    public class EditorBridgeBridgeImpl : EditorBridge.IEditorBridgeImpl
    {
        const string k_GameViewSizeName = "Graphic Test";
        const string k_VolumeProfilesDirectory = "Assets/Settings/PostEffects";

        public void SetGameViewSize(int width, int height)
        {
            if (GameViewUtils.SizeExists(GameViewSizeGroupType.Standalone, k_GameViewSizeName))
            {
                GameViewUtils.RemoveCustomSize(GameViewSizeGroupType.Standalone, GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
            }

            GameViewUtils.AddCustomSize(
                GameViewUtils.GameViewSizeType.FixedResolution,
                GameViewSizeGroupType.Standalone,
                width, height, k_GameViewSizeName);

            GameViewUtils.SetSize(GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
        }
        
        public VolumeProfile LoadVolumeProfile(string profileName)
        {
            var path = $"{k_VolumeProfilesDirectory}/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.IsNotNull(profile, $"Could not load volume profile at path \"{path}\"");
            return profile;
        }

        [InitializeOnLoadMethod]
        static void BindImplementation()
        {
            EditorBridge.SetImpl(new EditorBridgeBridgeImpl());
        }
    }
}
