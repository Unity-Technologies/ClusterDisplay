using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class StitcherLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderContext m_Context;
        RenderTexture[] m_SourceRts;

        public LayoutMode LayoutMode => LayoutMode.StandardStitcher;
        
        public StitcherLayoutBuilder(ClusterRenderContext context) => m_Context = context;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
        }

        /// <summary>
        /// Where rendering actually occurs.
        /// </summary>
        public void Render(Camera camera, RenderContext renderContext)
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, renderContext.numTiles, "Source", 
                renderContext.overscannedSize.x, 
                renderContext.overscannedSize.y, k_DefaultFormat);

            for (var tileIndex = 0; tileIndex != renderContext.numTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = renderContext.viewport.GetSubsectionWithOverscan(tileIndex);
                
                var asymmetricProjectionMatrix = renderContext.asymmetricProjection.GetFrustumSlice(overscannedViewportSubsection);

                var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);
                
                LayoutBuilderUtils.Render(camera, asymmetricProjectionMatrix, clusterParams, m_SourceRts[tileIndex]);
            }
        }

        public IEnumerable<BlitCommand> Present(RenderContext renderContext)
        {
            // No render happened, or grid size changed, cannot present.
            if (m_SourceRts == null || m_SourceRts.Length != renderContext.numTiles)
            {
                yield break;
            }
            
            for (var tileIndex = 0; tileIndex != renderContext.numTiles; ++tileIndex)
            {
                var viewportSubsection = renderContext.viewport.GetSubsectionWithoutOverscan(tileIndex);
                renderContext.blitParams.GetScaleBias(viewportSubsection, out var scaleBiasTex, out var scaleBiasRT);

                yield return new BlitCommand(m_SourceRts[tileIndex], scaleBiasTex, scaleBiasRT);
            }
        }
    }
}
