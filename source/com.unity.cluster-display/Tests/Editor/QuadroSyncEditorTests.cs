#if UNITY_EDITOR_WIN

using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Unity.ClusterDisplay;
using System;

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
            var d11 = GfxPluginQuadroSyncSystem.GfxPluginQuadroSyncUtilities.GetRenderEventFuncD3D11();
            Assert.IsNotNull(d11);
            var d12 = GfxPluginQuadroSyncSystem.GfxPluginQuadroSyncUtilities.GetRenderEventFuncD3D12();
            Assert.IsNotNull(d12);
        }
    }
}
#endif