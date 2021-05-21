using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherLayoutBuilder : LayoutBuilder
    {
        protected struct StitcherParameters
        {
            public object targetRT;
            public Vector4 scaleBiasTex;
            public Vector4 scaleBiasRT;
            public Rect percentageViewportSubsection;
        }

        protected Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        protected void CalculcateAndQueueStitcherParameters (object targetRT, Rect m_OverscannedRect, Rect percentageViewportSubsection)
        {
            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            
            var scaleBiasRT = new Vector4(
                1 - (k_ClusterRenderer.context.bezel.x * 2) / croppedSize.x, 1 - (k_ClusterRenderer.context.bezel.y * 2) / croppedSize.y, // scale
                k_ClusterRenderer.context.bezel.x / croppedSize.x, k_ClusterRenderer.context.bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                scaleBiasTex = scaleBiasTex,
                scaleBiasRT = scaleBiasRT,
                percentageViewportSubsection = percentageViewportSubsection,
                targetRT = targetRT,
            });
        }

        protected Matrix4x4 CalculateProjectionMatrix (Camera camera, Rect viewportSubsection) => GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
        protected void CalculateStitcherLayout(
            Camera camera,
            int i,
            ref ScriptableCullingParameters cullingParams,
            out Rect percentageViewportSubsection,
            out Rect viewportSubsection,
            out Matrix4x4 projectionMatrix)
        {
            percentageViewportSubsection = k_ClusterRenderer.context.GetViewportSubsection(i);
            viewportSubsection = percentageViewportSubsection;
            if (k_ClusterRenderer.context.physicalScreenSize != Vector2Int.zero && k_ClusterRenderer.context.bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.context.physicalScreenSize, k_ClusterRenderer.context.bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.context.overscanInPixels);

            projectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            cullingParams.stereoProjectionMatrix = projectionMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
        }
    }
}
