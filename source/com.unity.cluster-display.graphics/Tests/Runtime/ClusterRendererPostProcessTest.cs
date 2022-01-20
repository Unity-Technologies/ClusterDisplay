using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class ClusterRendererPostProcessTest : ClusterRendererTest
    {
        const string k_VolumeProfilesDirectory = "Assets/Settings/PostEffects";

        protected Volume m_Volume;

        protected override void InitializeTest()
        {
            base.InitializeTest();
            m_Volume = FindObjectOfType<Volume>();
            Assert.IsNotNull(m_Volume, $"Could not find ${nameof(Volume)}");
        }

        protected static VolumeProfile LoadVolumeProfile(string profileName)
        {
            var path = $"{k_VolumeProfilesDirectory}/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.IsNotNull(profile, $"Could not load volume profile at path \"{path}\"");
            return profile;
        }
    }
}
