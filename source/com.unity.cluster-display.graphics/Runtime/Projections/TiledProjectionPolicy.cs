using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

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
        public Vector2Int gridSize;

        /// <summary>
        /// Bezel of the screen, expressed in mm.
        /// </summary>
        public Vector2 bezel;
        
        /// <summary>
        /// Physical size of the screen in mm. Used to compute bezel.
        /// </summary>
        public Vector2 physicalScreenSize;
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
        public int tileIndexOverride;
        
        /// <summary>
        /// Use the viewport defined by <see cref="viewportSubsection"/> instead of the
        /// viewport inferred from the grid.
        /// </summary>
        public bool useDebugViewportSubsection;

        /// <summary>
        /// When <see cref="useDebugViewportSubsection"/> is <see langword="true"/>,
        /// use this viewport instead of the viewport inferred from the grid.
        /// </summary>
        public Rect viewportSubsection;

        /// <summary>
        /// Setting the layout mode to <see cref="LayoutMode.StandardStitcher"/> lets you see a
        /// simulation of the entire grid in the game view.
        /// </summary>
        public LayoutMode layoutMode;

        /// <summary>
        /// Color of the simulated bezels in <see cref="LayoutMode.StandardStitcher"/>.
        /// </summary>
        public Color bezelColor;

        /// <summary>
        /// Allows you to offset the presented image so you can see the overscan effect.
        /// </summary>
        public Vector2 scaleBiasTextOffset;
        
        /// <summary>
        /// Enable/Disable shader features, such as Global Screen Space,
        /// meant to compare original and ported-to-cluster-display shaders,
        /// in order to observe tiled projection artefacts.
        /// </summary>
        public bool enableKeyword;
    }
    
    [ExecuteAlways]
    public class TiledProjectionPolicy : MonoBehaviour, IProjectionPolicy
    {
        [SerializeField]
        TiledProjectionSettings m_Settings;

        [SerializeField]
        TiledProjectionDebugSettings m_DebugSettings;

        [SerializeField]
        bool m_IsDebug;

        readonly SlicedFrustumGizmo m_Gizmo = new SlicedFrustumGizmo();
        RenderTexture[] m_TileRenderTargets;
        readonly List<BlitCommand> m_BlitCommands = new List<BlitCommand>();
        
        GraphicsFormat m_GraphicsFormat;

        public TiledProjectionDebugSettings DebugSettings => m_DebugSettings;

        void OnDisable()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_TileRenderTargets);
            
            // TODO Do we assume *one* ClusterRenderer? If not, how do we manage the shader keyword?
            GraphicsUtil.SetShaderKeyword(false);
        }

        public void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            // Move early return at the Update's top.
            if (!(m_Settings.gridSize.x > 0 && m_Settings.gridSize.y > 0))
            {
                return;
            }
            
            GraphicsUtil.SetShaderKeyword(!m_IsDebug || m_DebugSettings.enableKeyword);
            
            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = m_IsDebug || !ClusterSync.Active ? m_DebugSettings.tileIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
            var numTiles = m_Settings.gridSize.x * m_Settings.gridSize.y;
            var displayMatrixSize = new Vector2Int(m_Settings.gridSize.x * displaySize.x, m_Settings.gridSize.y * displaySize.y);
            
            // Aspect must be updated *before* we pull the projection matrix.
            activeCamera.aspect = displayMatrixSize.x / (float)displayMatrixSize.y;
            var originalProjectionMatrix = activeCamera.projectionMatrix;
                        
#if UNITY_EDITOR
            m_Gizmo.tileIndex = currentTileIndex;
            m_Gizmo.gridSize = m_Settings.gridSize;
            m_Gizmo.viewProjectionInverse = (originalProjectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(m_Settings.gridSize, m_Settings.physicalScreenSize, m_Settings.bezel, clusterSettings.OverScanInPixels);
            var blitParams = new BlitParams(displaySize, clusterSettings.OverScanInPixels, 
                m_IsDebug ? m_DebugSettings.scaleBiasTextOffset : Vector2.zero);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize, m_Settings.gridSize);

            var renderContext = new RenderContext
            {
                currentTileIndex = currentTileIndex,
                numTiles = numTiles,
                overscannedSize = overscannedSize,
                viewport = viewport,
                originalProjection = originalProjectionMatrix,
                blitParams = blitParams,
                postEffectsParams = postEffectsParams,
                debugViewportSubsection = m_DebugSettings.viewportSubsection,
                useDebugViewportSubsection = m_IsDebug && m_DebugSettings.useDebugViewportSubsection
            };

            // Allocate tiles targets.
            var isStitcher = m_IsDebug && m_DebugSettings.layoutMode == LayoutMode.StandardStitcher;
            var numTargets = isStitcher ? renderContext.numTiles : 1;
            
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            GraphicsUtil.AllocateIfNeeded(ref m_TileRenderTargets, numTargets, "Source", 
                renderContext.overscannedSize.x, 
                renderContext.overscannedSize.y, m_GraphicsFormat);

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

        public void Present(CommandBuffer commandBuffer)
        {
            if (m_IsDebug && m_DebugSettings.layoutMode == LayoutMode.StandardStitcher)
            {
                commandBuffer.ClearRenderTarget(true, true, m_DebugSettings.bezelColor);
            }
            foreach (var command in m_BlitCommands)
            {
                GraphicsUtil.Blit(commandBuffer, command);
            }
        }
        
        
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (enabled)
            {
                m_Gizmo.Draw();
            }
        }
#endif
        
        static void RenderStitcher(IReadOnlyList<RenderTexture> targets, Camera camera, ref RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            for (var tileIndex = 0; tileIndex != renderContext.numTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = renderContext.viewport.GetSubsectionWithOverscan(tileIndex);

                var asymmetricProjectionMatrix = renderContext.originalProjection.GetFrustumSlice(overscannedViewportSubsection);

                var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);

                cameraScope.Render(asymmetricProjectionMatrix, clusterParams, targets[tileIndex]);

                var viewportSubsection = renderContext.viewport.GetSubsectionWithoutOverscan(tileIndex);

                commands.Add(new BlitCommand(targets[tileIndex], renderContext.blitParams.ScaleBias, GraphicsUtil.ToVector4(viewportSubsection)));
            }
        }

        static void RenderTile(RenderTexture target, Camera camera, ref RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            var overscannedViewportSubsection = renderContext.useDebugViewportSubsection ? 
                renderContext.debugViewportSubsection : 
                renderContext.viewport.GetSubsectionWithOverscan(renderContext.currentTileIndex);
            
            var asymmetricProjectionMatrix = renderContext.originalProjection.GetFrustumSlice(overscannedViewportSubsection);

            var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);
            
            cameraScope.Render(asymmetricProjectionMatrix, clusterParams, target);

            commands.Add(new BlitCommand(target, renderContext.blitParams.ScaleBias, GraphicsUtil.ToVector4(new Rect(0, 0, 1, 1))));
        }

    }
}