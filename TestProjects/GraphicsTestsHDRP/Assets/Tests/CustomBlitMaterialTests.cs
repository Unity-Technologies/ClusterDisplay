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
    const string k_ModifiedBlitShaderName = "Hidden/Test/Modified Blit";
    const string _DisplayChecker = "_DisplayChecker";
    const string _CheckerTexture = "_CheckerTexture";

    static Texture2D CheckerTexture => Resources.Load<Texture2D>("checker-with-crosshair");

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
            var cameraTransform = m_Camera.transform;
            var projection = m_ClusterRenderer.ProjectionPolicy;
            projection.SetCustomBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlock: null);
        });
    }

    static void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
    {
        if (hdCamera.camera != Camera.main)
        {
            return;
        }

        var colorBuffer = hdCamera.camera
            .GetComponent<HDAdditionalCameraData>()
            .GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color);

        var cmd = CommandBufferPool.Get("PresentCheckerBoard");
        // Blit the texture with a flip since were in HDRP.
        cmd.Blit(CheckerTexture, colorBuffer, new Vector2(2f, -2f), Vector2.zero);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    [UnityTest]
    public IEnumerator UseBlitMaterialWithMaterialPropertyBlock()
    {
        yield return RenderAndCompare(() =>
        {
            var additionalCameraData = m_Camera.GetComponent<HDAdditionalCameraData>();
            additionalCameraData.customRender -= OnCustomRender;
            additionalCameraData.customRender += OnCustomRender;

            var cameraTransform = m_Camera.transform;
            var projection = m_ClusterRenderer.ProjectionPolicy;

            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetInt(_DisplayChecker, 1);
            materialPropertyBlock.SetTexture(_CheckerTexture, CheckerTexture);

            projection.SetCustomBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlock);
        }, () =>
        {
            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlock: null);
            var additionalCameraData = m_Camera.GetComponent<HDAdditionalCameraData>();
            additionalCameraData.customRender -= OnCustomRender;
        });
    }

    [UnityTest]
    public IEnumerator UseBlitMaterialWithMaterialPropertyBlocks()
    {
        yield return RenderAndCompare(() =>
        {
            var additionalCameraData = m_Camera.GetComponent<HDAdditionalCameraData>();
            additionalCameraData.customRender -= OnCustomRender;
            additionalCameraData.customRender += OnCustomRender;

            var cameraTransform = m_Camera.transform;

            var materialPropertyBlocks = new Dictionary<int, MaterialPropertyBlock>()
            {
                {0, new MaterialPropertyBlock() },
                {1, new MaterialPropertyBlock() },
                {2, new MaterialPropertyBlock() },
                {3, new MaterialPropertyBlock() }
            };

            materialPropertyBlocks[0].SetInt(_DisplayChecker, 1);
            materialPropertyBlocks[0].SetTexture(_CheckerTexture, CheckerTexture);

            materialPropertyBlocks[1].SetInt(_DisplayChecker, 1);
            materialPropertyBlocks[1].SetTexture(_CheckerTexture, CheckerTexture);

            materialPropertyBlocks[2].SetInt(_DisplayChecker, 1);
            materialPropertyBlocks[2].SetTexture(_CheckerTexture, CheckerTexture);

            materialPropertyBlocks[3].SetInt(_DisplayChecker, 1);
            materialPropertyBlocks[3].SetTexture(_CheckerTexture, CheckerTexture);

            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlocks);
        }, () =>
        {
            m_ClusterRenderer.ProjectionPolicy.SetCustomBlitMaterial(new Material(Shader.Find(k_ModifiedBlitShaderName)), materialPropertyBlocks: null);
            var additionalCameraData = m_Camera.GetComponent<HDAdditionalCameraData>();
            additionalCameraData.customRender -= OnCustomRender;
        });
    }
}
