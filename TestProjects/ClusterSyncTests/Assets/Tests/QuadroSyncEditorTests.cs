#if UNITY_EDITOR_WIN

using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Unity.ClusterDisplay;
using System;
using System.Linq;
using Random = UnityEngine.Random;

namespace Unity.ClusterDisplay.Tests
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
        public void ExerciseFetchState()
        {
            // The goal of this test is to exercise the FetchState method and be sure it does not crash, hang or produce
            // completely bogus output.
            var state = GfxPluginQuadroSyncSystem.FetchState();

            // Initialization state might be different things (depending on the state of the engine, hardware present on
            // the computer, ... but in any case it should be a valid enum value.
            Assert.IsTrue(Enum.IsDefined(state.InitializationState.GetType(), state.InitializationState));
            // For now (current GfxPluginQuadro implementation & Nvidia API) swap group identifier is always be 0 or 1
            Assert.IsTrue((state.SwapGroupId == 0) || (state.SwapGroupId == 1));
            // For now (current GfxPluginQuadro implementation & Nvidia API) swap barrier identifier is always be 0 or 1
            Assert.IsTrue((state.SwapGroupId == 0) || (state.SwapGroupId == 1));
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
