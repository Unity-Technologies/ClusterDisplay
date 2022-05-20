using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class CustomBlitMaterialTest : ClusterRendererTest
    {
        const string k_CustomBlitShaderName = "Hidden/Test/Custom Blit";
        const string k_checkerTextureName = "checker-with-crosshair-noalpha";

        int _DisplayChecker = Shader.PropertyToID("_DisplayChecker");
        int _CheckerTexture = Shader.PropertyToID("_CheckerTexture");

        static Texture2D m_CheckerTexture;
        static Texture2D CheckerTexture
        {
            get
            {
                if (m_CheckerTexture == null)
                {
                    m_CheckerTexture = Resources.Load<Texture2D>(k_checkerTextureName);
                    if (m_CheckerTexture == null)
                    {
                        throw new System.NullReferenceException($"Unable to find checker texture in resources with name: \"{k_checkerTextureName}\".");
                    }
                }

                return m_CheckerTexture;
            }
        }

        protected Texture2D ReformatCheckerTexture ()
        {
            var reformatRT = new RenderTexture(m_ClusterCapture);

            UnityEngine.Graphics.Blit(CheckerTexture, reformatRT, new Vector2(2f, -2f), Vector2.zero);
            var resizedCheckerTexture = new Texture2D(CheckerTexture.width, CheckerTexture.height, m_ClusterCaptureTex2D.format, false);
            GraphicsTestUtil.CopyToTexture2D(reformatRT, resizedCheckerTexture);

            reformatRT.Release();

            return resizedCheckerTexture;
        }

        protected void TearDown ()
        {
            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(null, materialPropertyBlock: null);
            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(null, materialPropertyBlocks: null);
        }

        protected IEnumerator TestCustomBlitMaterial ()
        {
            yield return RenderAndCompare(() =>
            {
                var projection = m_ClusterRenderer.ProjectionPolicy;
                projection.SetCustomBlitMaterial(CoreUtils.CreateEngineMaterial(k_CustomBlitShaderName), materialPropertyBlock: null);
            });
        }

        protected IEnumerator TestCustomBlitMaterialWithMaterialPropertyBlock()
        {
            InitializeTest();

            var projection = m_ClusterRenderer.ProjectionPolicy;

            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetInt(_DisplayChecker, 1);
            materialPropertyBlock.SetTexture(_CheckerTexture, CheckerTexture);

            projection.SetCustomBlitMaterial(CoreUtils.CreateEngineMaterial(k_CustomBlitShaderName), materialPropertyBlock);

            yield return GraphicsTestUtil.PreWarm();
            yield return RenderOverscan();

            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);
            var reformattedCheckerTexture = ReformatCheckerTexture();

            ImageAssert.AreEqual(reformattedCheckerTexture, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
        }

        protected IEnumerator TestCustomBlitMaterialWithMaterialPropertyBlocks()
        {
            InitializeTest();

            var materialPropertyBlocks = new Dictionary<int, MaterialPropertyBlock>()
            {
                {0, new MaterialPropertyBlock() },
                {1, new MaterialPropertyBlock() },
                {2, new MaterialPropertyBlock() },
                {3, new MaterialPropertyBlock() }
            };

            for (int i = 0; i < 4; i++)
            {
                materialPropertyBlocks[i].SetInt(_DisplayChecker, 1);
                materialPropertyBlocks[i].SetTexture(_CheckerTexture, CheckerTexture);
            }

            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(CoreUtils.CreateEngineMaterial(k_CustomBlitShaderName), materialPropertyBlocks);

            yield return GraphicsTestUtil.PreWarm();
            yield return RenderOverscan();

            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);
            var reformattedCheckerTexture = ReformatCheckerTexture();

            ImageAssert.AreEqual(reformattedCheckerTexture, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
        }
    }
}
