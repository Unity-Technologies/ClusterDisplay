using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    abstract class StitcherLayoutBuilder : LayoutBuilder
    {
        protected struct StitcherParameters
        {
            public int TileIndex;
            public object SourceRT;
            public Vector4 ScaleBiasTex;
            public Vector4 ScaleBiasRT;
        }

        protected Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }

        protected void CalculcateAndQueueStitcherParameters<T>(int tileIndex, T targetRT, Rect m_OverscannedRect, Rect percentageViewportSubsection)
        {
            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.Context.OverscanInPixels, k_ClusterRenderer.Context.DebugScaleBiasTexOffset);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.Context.OverscanInPixels);

            var scaleBiasRT = new Vector4(
                1 - k_ClusterRenderer.Context.Bezel.x * 2 / croppedSize.x, 1 - k_ClusterRenderer.Context.Bezel.y * 2 / croppedSize.y, // scale
                k_ClusterRenderer.Context.Bezel.x / croppedSize.x, k_ClusterRenderer.Context.Bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                TileIndex = tileIndex,
                ScaleBiasTex = scaleBiasTex,
                ScaleBiasRT = scaleBiasRT,
                SourceRT = targetRT
            });
        }

        protected void CalculateStitcherLayout(
            Matrix4x4 cameraProjectionMatrix,
            int tileIndex,
            out Rect percentageViewportSubsection,
            out Rect viewportSubsection,
            out Matrix4x4 asymmetricProjectionMatrix)
        {
            percentageViewportSubsection = k_ClusterRenderer.Context.GetViewportSubsection(tileIndex);
            viewportSubsection = percentageViewportSubsection;
            if (k_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && k_ClusterRenderer.Context.Bezel != Vector2Int.zero)
            {
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.Context.PhysicalScreenSize, k_ClusterRenderer.Context.Bezel);
            }

            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.Context.OverscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(cameraProjectionMatrix, viewportSubsection);
        }
    }
}
