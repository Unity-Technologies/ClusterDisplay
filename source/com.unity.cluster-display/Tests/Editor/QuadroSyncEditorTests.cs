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
            var quadroSync = GfxPluginQuadroSyncSystem.GfxPluginQuadroSyncUtilities.GetRenderEventFunc();
            Assert.IsNotNull(quadroSync);
        }
    }
}
#endif