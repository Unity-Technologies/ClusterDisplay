using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class OverridingBlitMaterialTests : ClusterRendererPostProcessTest
    {
        const string k_ModifiedBlitShaderName = "Hidden/Test/Modified Blit";
        const string _DisplayRed = "_DisplayRed";

        [OneTimeSetUp]
        public void LoadScene()
        {
            SceneManager.LoadScene("OverrideBlitMaterial");
        }

        [UnityTest]
        public IEnumerator UseDefaultBlitMaterial()
        {
            yield return RenderAndCompare(() =>
            {
                var cameraTransform = m_Camera.transform;
                var projection = m_ClusterRenderer.ProjectionPolicy;
                projection.SetOverridingBlitMaterial(GraphicsUtil.GetBlitMaterial(), materialPropertyBlock: null);
            });
        }

        [UnityTest]
        public IEnumerator UseCustomBlitMaterial()
        {
            yield return RenderAndCompare(() =>
            {
                var cameraTransform = m_Camera.transform;
                var projection = m_ClusterRenderer.ProjectionPolicy;
                projection.SetOverridingBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlock: null);
            });
        }

        [UnityTest]
        public IEnumerator UseBlitMaterialWithMaterialPropertyBlock()
        {
            int cullingMask = m_Camera.cullingMask;
            yield return RenderAndCompare(() =>
            {
                m_Camera.cullingMask = LayerMask.NameToLayer("Nothing");
                var cameraTransform = m_Camera.transform;
                var projection = m_ClusterRenderer.ProjectionPolicy;

                var materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetInt(_DisplayRed, 1);

                projection.SetOverridingBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlock);
            }, () =>
            {
                m_Camera.cullingMask = cullingMask;
            });
        }
    }
}
