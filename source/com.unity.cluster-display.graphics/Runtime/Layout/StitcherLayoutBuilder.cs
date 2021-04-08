using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherLayoutBuilder : LayoutBuilder
    {
        protected RTHandle[] m_Targets;
        protected struct StitcherParameters
        {
            public RTHandle target;
            public Vector4 scaleBiasTex;
            public Vector4 scaleBiasRT;
            public Rect percentageViewportSubsection;
        }

        protected Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        protected void ReleaseTargets()
        {
            if (m_Targets != null)
            {
                for (var i = 0; i != m_Targets.Length; ++i)
                {
                    RTHandles.Release(m_Targets[i]);
                    m_Targets[i] = null;
                }
            }

            m_Targets = null;
        }

        protected void PollRTs ()
        {
            if (!ValidGridSize(out var numTiles))
                return;

            if (m_Targets != null && m_Targets.Length == numTiles)
                return;
            m_Targets = new RTHandle[numTiles];
        }

        protected void PollRT (int i, Rect m_OverscannedRect)
        {
            bool resized = m_Targets[i] != null && 
                (m_Targets[i].rt.width != (int)m_OverscannedRect.x || 
                m_Targets[i].rt.height != (int)m_OverscannedRect.y);

            if (m_Targets[i] == null || resized)
            {
                if (m_Targets[i] != null)
                    RTHandles.Release(m_Targets[i]);

                m_Targets[i] = RTHandles.Alloc(
                    width: (int)m_OverscannedRect.width,
                    height: (int)m_OverscannedRect.height,
                    slices: 1,
                    dimension: TextureXR.dimension,
                    useDynamicScale: false,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    // msaaSamples: MSAASamples.MSAA8x,
                    name: $"Tile Target {i}");
            }
        }

        protected void CalculcateAndQueueStitcherParameters (int tileIndex, Rect m_OverscannedRect, Rect percentageViewportSubsection)
        {
            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
            
            var scaleBiasRT = new Vector4(
                1 - (m_ClusterRenderer.Context.Bezel.x * 2) / croppedSize.x, 1 - (m_ClusterRenderer.Context.Bezel.y * 2) / croppedSize.y, // scale
                m_ClusterRenderer.Context.Bezel.x / croppedSize.x, m_ClusterRenderer.Context.Bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                scaleBiasTex = scaleBiasTex,
                scaleBiasRT = scaleBiasRT,
                percentageViewportSubsection = percentageViewportSubsection,
                target = m_Targets[tileIndex]
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
            percentageViewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection(i);
            viewportSubsection = percentageViewportSubsection;
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            projectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            cullingParams.stereoProjectionMatrix = projectionMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
        }
    }
}
