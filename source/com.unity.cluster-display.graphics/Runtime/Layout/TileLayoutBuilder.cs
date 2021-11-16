using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class TileLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        RenderTexture m_SourceRt;

        public LayoutMode LayoutMode => LayoutMode.StandardTile;
        
        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRt);
        }
        
        public void Render(Camera camera, RenderContext renderContext)
        {
            var overscannedViewportSubsection = renderContext.useDebugViewportSubsection ? 
                renderContext.debugViewportSubsection : 
                renderContext.viewport.GetSubsectionWithOverscan(renderContext.currentTileIndex);
            
            var asymmetricProjectionMatrix = renderContext.asymmetricProjection.GetFrustumSlice(overscannedViewportSubsection);

            var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);
            
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRt, "Source", 
                renderContext.overscannedSize.x, 
                renderContext.overscannedSize.y, k_DefaultFormat);

            LayoutBuilderUtils.Render(camera, asymmetricProjectionMatrix, clusterParams, m_SourceRt);
        }

        public IEnumerable<BlitCommand> Present(RenderContext renderContext)
        {
            // No render happened, cannot present.
            if (m_SourceRt == null)
            {
                yield break;
            }
        
            renderContext.blitParams.GetScaleBias(new Rect(0, 0, 1, 1), out var scaleBiasTex, out var scaleBiasRT);

            yield return new BlitCommand(m_SourceRt, scaleBiasTex, scaleBiasRT);
        }
    }
}
