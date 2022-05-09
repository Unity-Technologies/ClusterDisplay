using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEngine;

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
        /// Clear color used to present to screen.
        /// </summary>
        public Color PresentClearColor;

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
    [CreateAssetMenu(fileName = "TiledProjection", menuName = "Cluster Display/Tiled Projection")]
    sealed class TiledProjection : ProjectionPolicy
    {
        [SerializeField]
        TiledProjectionSettings m_Settings = new() { GridSize = new Vector2Int(2, 2), PhysicalScreenSize = new Vector2(1600, 900) };

        [SerializeField]
        TiledProjectionDebugSettings m_DebugSettings = new() { ViewportSubsection = new Rect(0, 0, 0.5f, 0.5f) };

        readonly SlicedFrustumGizmo m_Gizmo = new SlicedFrustumGizmo();
        readonly List<BlitCommand> m_BlitCommands = new List<BlitCommand>();
        RenderTexture[] m_TileRenderTargets;
        Vector2Int m_DisplayMatrixSize = Vector2Int.one;

        TiledProjectionSettings? m_RuntimeSettings;

        public TiledProjectionSettings Settings
        {
            get => m_RuntimeSettings ?? m_Settings;
            set => m_RuntimeSettings = value;
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

        void OnDisable()
        {
            m_BlitCommands.Clear();
            GraphicsUtil.DeallocateIfNeeded(ref m_TileRenderTargets);

            m_RuntimeSettings = m_Settings;
        }

        private int GetTileIndex()
        {
            // TODO: Cluster settings do not change, so we should only need to initialize the tile index once.
            if (!m_IsDebug && ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.IsClusterLogicEnabled)
            {
                return CommandLineParser.replaceHeadlessEmitter.Value &&
                    clusterSync.NodeRole is NodeRole.Repeater &&
                    clusterSync.EmitterIsHeadless
                    ? clusterSync.NodeID - 1
                    : clusterSync.NodeID;
            }

            return m_DebugSettings.TileIndexOverride;
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            // Move early return at the Update's top.
            if (!(Settings.GridSize.x > 0 && Settings.GridSize.y > 0))
            {
                return;
            }

            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = GetTileIndex();
            var numTiles = Settings.GridSize.x * Settings.GridSize.y;
            m_DisplayMatrixSize = new Vector2Int(Settings.GridSize.x * displaySize.x, Settings.GridSize.y * displaySize.y);

            // Aspect must be updated *before* we pull the projection matrix.
            activeCamera.aspect = m_DisplayMatrixSize.x / (float)m_DisplayMatrixSize.y;
            var originalProjectionMatrix = activeCamera.projectionMatrix;

#if UNITY_EDITOR
            m_Gizmo.TileIndex = currentTileIndex;
            m_Gizmo.GridSize = Settings.GridSize;
            m_Gizmo.ViewProjectionInverse = (originalProjectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(Settings.GridSize, Settings.PhysicalScreenSize, Settings.Bezel, clusterSettings.OverScanInPixels);
            var blitParams = new BlitParams(displaySize, clusterSettings.OverScanInPixels,
                m_IsDebug ? m_DebugSettings.ScaleBiasTextOffset : Vector2.zero);
            var postEffectsParams = new PostEffectsParams(m_DisplayMatrixSize);

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
                UseDebugViewportSubsection = m_IsDebug && m_DebugSettings.LayoutMode == LayoutMode.StandardTile && m_DebugSettings.UseDebugViewportSubsection
            };

            // Allocate tiles targets.
            var isStitcher = m_IsDebug && m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher;
            var numTargets = isStitcher ? renderContext.NumTiles : 1;

            GraphicsUtil.AllocateIfNeeded(ref m_TileRenderTargets, numTargets,
                renderContext.OverscannedSize.x,
                renderContext.OverscannedSize.y,
                "Source");

            m_BlitCommands.Clear();

            if (isStitcher)
            {
                RenderStitcher(
                    m_TileRenderTargets,
                    activeCamera,
                    ref renderContext,
                    m_BlitCommands);
            }
            else
            {
                RenderTile(
                    m_TileRenderTargets[0],
                    activeCamera,
                    ref renderContext,
                    m_BlitCommands);
            }

            // TODO Make sure there's no one-frame offset induced by rendering timing.
            // TODO Make sure blitCommands are executed within the frame.
            // Screen camera must render *after* all tiles have been rendered.

            // TODO is it really needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        public override void Present(PresentArgs args)
        {
            if (m_IsDebug && m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher)
            {
                var presentRatio = args.CameraPixelRect.width / args.CameraPixelRect.height;
                var stitchedRatio = m_DisplayMatrixSize.x / (float)m_DisplayMatrixSize.y;

                if (!Mathf.Approximately(presentRatio, stitchedRatio))
                {
                    var pixelRect = args.CameraPixelRect;

                    if (stitchedRatio > presentRatio)
                    {
                        pixelRect.height = pixelRect.width / stitchedRatio;
                        pixelRect.y += (args.CameraPixelRect.height - pixelRect.height) / 2;
                    }
                    else
                    {
                        pixelRect.width = pixelRect.height * stitchedRatio;
                        pixelRect.x += (args.CameraPixelRect.width - pixelRect.width) / 2;
                    }

                    args.CommandBuffer.SetViewport(pixelRect);
                }
            }

            foreach (var command in m_BlitCommands)
            {
                GraphicsUtil.Blit(args.CommandBuffer, command, args.FlipY);
            }
        }

        public override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            m_Gizmo.Draw();
#endif
        }

        void RenderStitcher(
            IReadOnlyList<RenderTexture> targets,
            Camera camera,
            ref TileProjectionContext tileProjectionContext,
            List<BlitCommand> commands)
        {
            using var cameraScope = CameraScopeFactory.Create(
                camera,
                RenderFeature.AsymmetricProjectionAndScreenCoordOverride);

            for (var tileIndex = 0; tileIndex != tileProjectionContext.NumTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = tileProjectionContext.Viewport.GetSubsectionWithOverscan(tileIndex);

                var asymmetricProjectionMatrix = tileProjectionContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);

                var screenSizeOverride = tileProjectionContext.PostEffectsParams.GetScreenSizeOverride();

                var screenCoordScaleBias = PostEffectsParams.GetScreenCoordScaleBias(overscannedViewportSubsection);

                cameraScope.Render(targets[tileIndex], asymmetricProjectionMatrix, screenSizeOverride, screenCoordScaleBias);

                var viewportSubsection = tileProjectionContext.Viewport.GetSubsectionWithoutOverscan(tileIndex);

                commands.Add(new BlitCommand(targets[tileIndex], tileProjectionContext.BlitParams.ScaleBias, GraphicsUtil.AsScaleBias(viewportSubsection), GetOverridingBlitMaterial(), GetOverridingBlitPropertyBlock(tileIndex)));
            }
        }

        void RenderTile(
            RenderTexture target,
            Camera camera,
            ref TileProjectionContext tileProjectionContext,
            List<BlitCommand> commands)
        {
            using var cameraScope = CameraScopeFactory.Create(
                camera,
                RenderFeature.AsymmetricProjectionAndScreenCoordOverride);

            var overscannedViewportSubsection = tileProjectionContext.UseDebugViewportSubsection ? tileProjectionContext.DebugViewportSubsection : tileProjectionContext.Viewport.GetSubsectionWithOverscan(tileProjectionContext.CurrentTileIndex);

            var asymmetricProjectionMatrix = tileProjectionContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);

            var screenSizeOverride = tileProjectionContext.PostEffectsParams.GetScreenSizeOverride();

            var screenCoordScaleBias = PostEffectsParams.GetScreenCoordScaleBias(overscannedViewportSubsection);

            cameraScope.Render(target, asymmetricProjectionMatrix, screenSizeOverride, screenCoordScaleBias);

            commands.Add(new BlitCommand(target, tileProjectionContext.BlitParams.ScaleBias, GraphicsUtil.k_IdentityScaleBias, GetOverridingBlitMaterial(), GetOverridingBlitPropertyBlock(tileProjectionContext.CurrentTileIndex)));
        }
    }
}
