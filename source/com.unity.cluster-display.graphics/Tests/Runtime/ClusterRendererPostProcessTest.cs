using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class ClusterRendererPostProcessTest : ClusterRendererTest
    {

        protected Volume m_Volume;

        protected override void InitializeTest()
        {
            base.InitializeTest();
            m_Volume = FindObjectOfType<Volume>();
            Assert.IsNotNull(m_Volume, $"Could not find ${nameof(Volume)}");
        }

        protected static VolumeProfile LoadVolumeProfile(string profileName)
        {
            var profile = EditorBridge.LoadVolumeProfile(profileName);
            Assert.IsNotNull(profile);
            return profile;
        }
    }
}
