using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// User settings for Tiled Projection Rendering
    /// </summary>
    [Serializable]
    public struct TiledProjectionSettings
    {
        /// <summary>
        /// Cluster Grid size expressed in tiles.
        /// </summary>
        public Vector2Int GridSize;

        /// <summary>
        /// Bezel of the screen, expressed in mm.
        /// </summary>
        public Vector2 Bezel;

        /// <summary>
        /// Physical size of the screen in mm. Used to compute bezel.
        /// </summary>
        public Vector2 PhysicalScreenSize;
    }

    /// <summary>
    /// Debug settings for the Tiled Projection rendering.
    /// Those are both meant to debug the ClusterRenderer itself,
    /// and external graphics related ClusterDisplay code.
    /// </summary>
    [Serializable]
    public struct TiledProjectionDebugSettings
    {
        /// <summary>
        /// Render the tile at this index of the index determined by the node id.
        /// </summary>
        public int TileIndexOverride;

        /// <summary>
        /// Use the viewport defined by <see cref="ViewportSubsection"/> instead of the
        /// viewport inferred from the grid.
        /// </summary>
        public bool UseDebugViewportSubsection;

        /// <summary>
        /// When <see cref="UseDebugViewportSubsection"/> is <see langword="true"/>,
        /// use this viewport instead of the viewport inferred from the grid.
        /// </summary>
        public Rect ViewportSubsection;

        /// <summary>
        /// Setting the layout mode to <see cref="ClusterDisplay.Graphics.LayoutMode.StandardStitcher"/> lets you see a
        /// simulation of the entire grid in the game view.
        /// </summary>
        public LayoutMode LayoutMode;

        /// <summary>
        /// Color of the simulated bezels in <see cref="ClusterDisplay.Graphics.LayoutMode.StandardStitcher"/>.
        /// </summary>
        public Color BezelColor;

        /// <summary>
        /// Allows you to offset the presented image so you can see the overscan effect.
        /// </summary>
        public Vector2 ScaleBiasTextOffset;

        /// <summary>
        /// Enable/Disable shader features, such as Global Screen Space,
        /// meant to compare original and ported-to-cluster-display shaders,
        /// in order to observe tiled projection artefacts.
        /// </summary>
        public bool EnableKeyword;
    }

    [PopupItem("Tiled")]
    public sealed class TiledProjection : ProjectionPolicy
    {
        [SerializeField]
        TiledProjectionSettings m_Settings = new() {GridSize = new Vector2Int(2, 2), PhysicalScreenSize = new Vector2(1600, 900)};

        [SerializeField]
        TiledProjectionDebugSettings m_DebugSettings = new() {ViewportSubsection = new Rect(0, 0, 0.5f, 0.5f)};

        [SerializeField]
        bool m_IsDebug;

        readonly SlicedFrustumGizmo m_Gizmo = new SlicedFrustumGizmo();
        RenderTexture[] m_TileRenderTargets;
        readonly List<BlitCommand> m_BlitCommands = new List<BlitCommand>();

        GraphicsFormat m_GraphicsFormat;

        public TiledProjectionSettings Settings
        {
            get => m_Settings;
            internal set => m_Settings = value;
        }

        public ref readonly TiledProjectionDebugSettings DebugSettings => ref m_DebugSettings;

        ref struct TileProjectionContext
        {
            public int CurrentTileIndex;
            public int NumTiles;
            public Vector2Int OverscannedSize;
            public Viewport Viewport;
            public Matrix4x4 OriginalProjection;
            public BlitParams BlitParams;
            public PostEffectsParams PostEffectsParams;

            // Debug data.
            public Rect DebugViewportSubsection;
            public bool UseDebugViewportSubsection;
        }

        void OnEnable()
        {
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        }

        void OnDisable()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_TileRenderTargets);

            // TODO Do we assume *one* ClusterRenderer? If not, how do we manage the shader keyword?
            GraphicsUtil.SetShaderKeyword(false);
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            // Move early return at the Update's top.
            if (!(m_Settings.GridSize.x > 0 && m_Settings.GridSize.y > 0))
            {
                return;
            }

            GraphicsUtil.SetShaderKeyword(!m_IsDebug || m_DebugSettings.EnableKeyword);

            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = m_IsDebug || !ClusterSync.Active ? m_DebugSettings.TileIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
            var numTiles = m_Settings.GridSize.x * m_Settings.GridSize.y;
            var displayMatrixSize = new Vector2Int(m_Settings.GridSize.x * displaySize.x, m_Settings.GridSize.y * displaySize.y);

            // Aspect must be updated *before* we pull the projection matrix.
            activeCamera.aspect = displayMatrixSize.x / (float) displayMatrixSize.y;
            var originalProjectionMatrix = activeCamera.projectionMatrix;

#if UNITY_EDITOR
            m_Gizmo.TileIndex = currentTileIndex;
            m_Gizmo.GridSize = m_Settings.GridSize;
            m_Gizmo.ViewProjectionInverse = (originalProjectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(m_Settings.GridSize, m_Settings.PhysicalScreenSize, m_Settings.Bezel, clusterSettings.OverScanInPixels);
            var blitParams = new BlitParams(displaySize, clusterSettings.OverScanInPixels,
                m_IsDebug ? m_DebugSettings.ScaleBiasTextOffset : Vector2.zero);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize, m_Settings.GridSize);

            var renderContext = new TileProjectionContext
            {
                CurrentTileIndex = currentTileIndex,
                NumTiles = numTiles,
                OverscannedSize = overscannedSize,
                Viewport = viewport,
                OriginalProjection = originalProjectionMatrix,
                BlitParams = blitParams,
                PostEffectsParams = postEffectsParams,
                DebugViewportSubsection = m_DebugSettings.ViewportSubsection,
                UseDebugViewportSubsection = m_IsDebug && m_DebugSettings.UseDebugViewportSubsection
            };

            // Allocate tiles targets.
            var isStitcher = m_IsDebug && m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher;
            var numTargets = isStitcher ? renderContext.NumTiles : 1;

            GraphicsUtil.AllocateIfNeeded(ref m_TileRenderTargets, numTargets,
                renderContext.OverscannedSize.x,
                renderContext.OverscannedSize.y,
                m_GraphicsFormat,
                "Source");

            m_BlitCommands.Clear();

            if (isStitcher)
            {
                RenderStitcher(m_TileRenderTargets, activeCamera, ref renderContext, m_BlitCommands);
            }
            else
            {
                RenderTile(m_TileRenderTargets[0], activeCamera, ref renderContext, m_BlitCommands);
            }

            // TODO Make sure there's no one-frame offset induced by rendering timing.
            // TODO Make sure blitCommands are executed within the frame.
            // Screeen camera must render *after* all tiles have been rendered.

            // TODO is it really needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        public override void Present(CommandBuffer commandBuffer)
        {
            if (m_IsDebug && m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher)
            {
                commandBuffer.ClearRenderTarget(true, true, m_DebugSettings.BezelColor);
            }

            foreach (var command in m_BlitCommands)
            {
                GraphicsUtil.Blit(commandBuffer, command);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            m_Gizmo.Draw();
        }
#endif

        static void RenderStitcher(IReadOnlyList<RenderTexture> targets, Camera camera, ref TileProjectionContext tileProjectionContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            for (var tileIndex = 0; tileIndex != tileProjectionContext.NumTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = tileProjectionContext.Viewport.GetSubsectionWithOverscan(tileIndex);

                var asymmetricProjectionMatrix = tileProjectionContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);

                var clusterParams = tileProjectionContext.PostEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);

                cameraScope.Render(asymmetricProjectionMatrix, clusterParams, targets[tileIndex]);

                var viewportSubsection = tileProjectionContext.Viewport.GetSubsectionWithoutOverscan(tileIndex);

                commands.Add(new BlitCommand(targets[tileIndex], tileProjectionContext.BlitParams.ScaleBias, GraphicsUtil.ToVector4(viewportSubsection)));
            }
        }

        static void RenderTile(RenderTexture target, Camera camera, ref TileProjectionContext tileProjectionContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            var overscannedViewportSubsection = tileProjectionContext.UseDebugViewportSubsection ? tileProjectionContext.DebugViewportSubsection : tileProjectionContext.Viewport.GetSubsectionWithOverscan(tileProjectionContext.CurrentTileIndex);

            var asymmetricProjectionMatrix = tileProjectionContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);

            var clusterParams = tileProjectionContext.PostEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);

            cameraScope.Render(asymmetricProjectionMatrix, clusterParams, target);

            commands.Add(new BlitCommand(target, tileProjectionContext.BlitParams.ScaleBias, GraphicsUtil.ToVector4(new Rect(0, 0, 1, 1))));
        }
    }
}
