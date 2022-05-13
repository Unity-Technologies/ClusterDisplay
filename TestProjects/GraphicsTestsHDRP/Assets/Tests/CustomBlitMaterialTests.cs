using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CustomBlitMaterialTests : ClusterRendererTestReferenceCamera
{
    const string k_CustomBlitShaderName = "Hidden/Test/Custom Blit";

    int _DisplayChecker = Shader.PropertyToID("_DisplayChecker");
    int _CheckerTexture = Shader.PropertyToID("_CheckerTexture");

    static Texture2D CheckerTexture => Resources.Load<Texture2D>("checker-with-crosshair-noalpha");

    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("CustomBlitMaterial");
    }

    [UnityTest]
    public IEnumerator UseCustomBlitMaterial()
    {
        yield return RenderAndCompare(() =>
        {
            var projection = m_ClusterRenderer.ProjectionPolicy;
            projection.SetCustomBlitMaterial(CoreUtils.CreateEngineMaterial(k_CustomBlitShaderName), materialPropertyBlock: null);
        }, () =>
        {
            var projection = m_ClusterRenderer.ProjectionPolicy;
            projection.SetCustomBlitMaterial(null, materialPropertyBlock: null);
        });
    }

    Texture2D ResizeCheckerTexture ()
    {
        var resizeRT = new RenderTexture(m_ClusterCapture);
        
        Graphics.Blit(CheckerTexture, resizeRT, new Vector2(2f, -2f), Vector2.zero);

        var resizedCheckerTexture = new Texture2D(m_ClusterCaptureTex2D.width, m_ClusterCaptureTex2D.height, m_ClusterCaptureTex2D.format, false);
        GraphicsTestUtil.CopyToTexture2D(resizeRT, resizedCheckerTexture);

        resizeRT.Release();

        return resizedCheckerTexture;
    }

    [UnityTest]
    public IEnumerator UseBlitMaterialWithMaterialPropertyBlock()
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
        var resizedCheckerTexture = ResizeCheckerTexture();

        ImageAssert.AreEqual(resizedCheckerTexture, m_ClusterCaptureTex2D, m_ImageComparisonSettings);

        m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(null, materialPropertyBlock: null);
    }

    [UnityTest]
    public IEnumerator UseBlitMaterialWithMaterialPropertyBlocks()
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
        var resizedCheckerTexture = ResizeCheckerTexture();
        ImageAssert.AreEqual(resizedCheckerTexture, m_ClusterCaptureTex2D, m_ImageComparisonSettings);

        m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(null, materialPropertyBlocks: null);
    }
}
