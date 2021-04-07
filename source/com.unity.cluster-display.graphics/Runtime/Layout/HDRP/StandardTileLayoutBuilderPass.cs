using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilderPass : CustomPass
    {
        // private RTHandle m_ColorCopy;
        // public Vector4 m_ScaleBiasTex;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd) 
        {
            /*
            var hdrpAsset = (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset);
            var colorBufferFormat = hdrpAsset.currentPlatformRenderPipelineSettings.colorBufferFormat;
            m_ColorCopy = RTHandles.Alloc(
                Vector2.one, 
                1, 
                dimension: TextureXR.dimension,
                colorFormat: (GraphicsFormat)colorBufferFormat,
                useDynamicScale: true, 
                name: "Color Copy");
            */
        }

        protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera camera, CullingResults cullingResult) 
        {
            /*
            GetCameraBuffers(out var cameraColor, out var cameraDepth);
            cmd.SetRenderTarget(m_ColorCopy);
            cmd.ClearRenderTarget(true, true, Color.black);
            HDUtils.BlitQuad(cmd, cameraColor, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0), 0, false);

            // cmd.SetViewport(m_Viewport);
            cmd.SetRenderTarget(cameraColor);
            cmd.ClearRenderTarget(true, true, Color.black);

            HDUtils.BlitQuad(cmd, m_ColorCopy, m_ScaleBiasTex, TileLayoutBuilder.k_ScaleBiasRT, 0, true);
            // HDUtils.DrawFullScreen(cmd, HDUtils.GetBlitMaterial(TextureDimension.Tex2D), m_ColorCopy);
            // renderContext.ExecuteCommandBuffer(cmd);
            */
        }

        protected override void Cleanup() 
        {
            // m_ColorCopy.Release();
        }
    }
}
