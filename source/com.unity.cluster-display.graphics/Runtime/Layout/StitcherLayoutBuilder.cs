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
            public int tileIndex;
            public object sourceRT;
            public Vector4 scaleBiasTex;
            public Vector4 scaleBiasRT;
            public Rect percentageViewportSubsection;
        }

        protected Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        protected void CalculcateAndQueueStitcherParameters<T> (int tileIndex, T targetRT, Rect m_OverscannedRect, Rect percentageViewportSubsection)
        {
            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            
            var scaleBiasRT = new Vector4(
                1 - (k_ClusterRenderer.context.bezel.x * 2) / croppedSize.x, 1 - (k_ClusterRenderer.context.bezel.y * 2) / croppedSize.y, // scale
                k_ClusterRenderer.context.bezel.x / croppedSize.x, k_ClusterRenderer.context.bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                tileIndex = tileIndex,
                scaleBiasTex = scaleBiasTex,
                scaleBiasRT = scaleBiasRT,
                percentageViewportSubsection = percentageViewportSubsection,
                sourceRT = targetRT,
            });
        }

        protected Matrix4x4 CalculateProjectionMatrix (Camera camera, Rect viewportSubsection) => GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
        protected void CalculateStitcherLayout(
            Camera camera,
            Matrix4x4 cameraProjectionMatrix,
            int i,
            ref ScriptableCullingParameters cullingParams,
            out Rect percentageViewportSubsection,
            out Rect viewportSubsection,
            out Matrix4x4 asymmetricProjectionMatrix)
        {
            percentageViewportSubsection = k_ClusterRenderer.context.GetViewportSubsection(i);
            viewportSubsection = percentageViewportSubsection;
            if (k_ClusterRenderer.context.physicalScreenSize != Vector2Int.zero && k_ClusterRenderer.context.bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.context.physicalScreenSize, k_ClusterRenderer.context.bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.context.overscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(cameraProjectionMatrix, viewportSubsection);

            cullingParams.stereoProjectionMatrix = asymmetricProjectionMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
        }

    }
}
