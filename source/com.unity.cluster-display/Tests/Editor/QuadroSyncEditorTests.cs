#if UNITY_EDITOR_WIN

using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Unity.ClusterDisplay;
using System;
using System.Linq;

namespace Unity.ClusterDisplay.EditorTest
{
    class QuadroSyncEditorTests
    {
        GameObject gameObject;

        [SetUp]
        public void Setup()
        {
            //gameObject = new GameObject();
            //gameObject.AddComponent<GfxPluginQuadroSyncCallbacks>();
        }

        [Ignore("Enable when Native Low Level plugin changes land in Trunk")]
        [UnityTest]
        public IEnumerator IsDLLFound()
        {
            yield return null;
            Assert.True(true);
        }

        [Test]
        public void LowLevelDLLFound()
        {
            var quadroSync = GfxPluginQuadroSyncSystem.GfxPluginQuadroSyncUtilities.GetRenderEventFunc();
            Assert.IsNotNull(quadroSync);
        }

        [Test]
        public void CanFetchState()
        {
            var state = GfxPluginQuadroSyncSystem.Instance.FetchState();

            // Initialization state might be different things (depending on the state of the engine, hardware present on
            // the computer, ... but in any case it should be a valid enum value.
            Assert.IsTrue(Enum.IsDefined(state.InitializationState.GetType(), state.InitializationState));
        }

        const string k_UnknownDescriptiveText = "Unknown initialization state";
        [Test]
        public void InitializationStateHasDescriptiveText()
        {
            var stateConstants =
                Enum.GetValues(typeof(GfxPluginQuadroSyncInitializationState)).Cast<GfxPluginQuadroSyncInitializationState>();
            foreach (var enumConstant in stateConstants)
            {
                var descriptiveText = enumConstant.ToDescriptiveText();
                Assert.IsNotEmpty(descriptiveText);
                Assert.AreNotEqual(k_UnknownDescriptiveText, descriptiveText);
            }

            // Last test, be sure that an unknown enum constant would give "Unknown initialization state"
            Assert.AreEqual(k_UnknownDescriptiveText,
                            ((GfxPluginQuadroSyncInitializationState)0xFFFFFF).ToDescriptiveText());
        }
    }
}
#endif
