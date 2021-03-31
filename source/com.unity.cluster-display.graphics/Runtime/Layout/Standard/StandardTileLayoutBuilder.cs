using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        RTHandle m_OverscannedTarget;
        Rect m_OverscannedRect;
        int m_OverscanInPixels;

        // Allow overscanned pixels visualization for debugging purposes.
        Vector2 m_DebugScaleBiasTexOffset;


        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public bool BuildLayout()
        {
            var camera = m_ClusterRenderer.CameraController.CameraContext;

            if (!SetupLayout(camera, out var cullingParams, out var projMatrix, out var viewportSubsection))
                return false;
            camera.projectionMatrix = projMatrix;

            return true;
        }

        public void BuildMirrorView()
        {
        }

        public override void Dispose()
        {
        }
    }
}
