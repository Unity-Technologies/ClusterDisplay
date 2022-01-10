using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// This component is responsible for managing projection, layout (tile, stitcher),
    /// and Cluster Display specific shader features such as Global Screen Space.
    /// </summary>
    /// <remarks>
    /// We typically expect at most one instance active at a given time.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterRenderer : MonoBehaviour
    {
        // Temporary, we need a way to *not* procedurally deactivate cameras when no cluster rendering occurs.
        static int S_ActiveInstancesCount;
        internal static bool IsActive() => S_ActiveInstancesCount > 0;
        internal static Action Enabled = delegate { };
        internal static Action Disabled = delegate { };
        
        // Will only be used for Legacy if we end up supporting it.
        // Otherwise see ScreenCoordOverrideUtils in SRP Core.
        const string k_ShaderKeyword = "SCREEN_COORD_OVERRIDE";
        internal static void EnableScreenCoordOverrideKeyword(bool enabled) => GraphicsUtil.SetShaderKeyword(k_ShaderKeyword, enabled);

        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }
        
        [SerializeField]
        readonly ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        [SerializeField]
        readonly ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();

        readonly List<BlitCommand> m_BlitCommands = new List<BlitCommand>();

        readonly List<ICapturePresent> m_PresentCaptures = new List<ICapturePresent>();

        internal void AddCapturePresent(ICapturePresent capturePresent)
        {
            if (!m_PresentCaptures.Contains(capturePresent))
            {
                m_PresentCaptures.Add(capturePresent);
            }
        }

        internal void RemoveCapturePresent(ICapturePresent capturePresent)
        {
            m_PresentCaptures.Remove(capturePresent);
        }
        
        RenderTexture[] m_TileRenderTargets;
        GraphicsFormat m_GraphicsFormat;
        bool m_IsDebug;

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif

        public bool IsDebug
        {
            get => m_IsDebug;
            set => m_IsDebug = value;
        }
        
        public ClusterRendererDebugSettings DebugSettings => m_DebugSettings;

        public ClusterRendererSettings Settings => m_Settings;


        readonly SlicedFrustumGizmo m_Gizmo = new SlicedFrustumGizmo();

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (enabled)
            {
                m_Gizmo.Draw();
            }
        }
#endif

        void OnEnable()
        {
            ++S_ActiveInstancesCount;

            // TODO More elegant / user friendly way to handle this.
            if (S_ActiveInstancesCount > 1)
            {
                throw new InvalidOperationException($"At most one instance of {nameof(ClusterRenderer)} can be active.");
            }
            
            // Sync, will change from inspector as well.
            // TODO We must set the keyword systematically unless in debug mode.
            
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            // TODO Keyword should be set for one render only at a time. Ex: not when rendering the scene camera.
            // EnableScreenCoordOverrideKeyword(m_DebugSettings.EnableKeyword);
            m_Presenter.Enable(gameObject);
            m_Presenter.Present += OnPresent;

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            // TODO Needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
            Enabled.Invoke();
        }

        void OnDisable()
        {
            Disabled.Invoke();
            
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            // TODO Do we assume *one* ClusterRenderer? If not, how do we manage the shader keyword?
            // EnableScreenCoordOverrideKeyword(false);
            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
            
            GraphicsUtil.DeallocateIfNeeded(ref m_TileRenderTargets);

            --S_ActiveInstancesCount;
        }

        void OnDestroy()
        {
            m_PresentCaptures.Clear();
        }

        void OnPresent(CommandBuffer commandBuffer)
        {
            ExecuteBlitCommands(commandBuffer);

            foreach (var capturePresent in m_PresentCaptures)
            {
                capturePresent.OnBeginCapture();
                ExecuteBlitCommands(capturePresent.GetCommandBuffer());
                capturePresent.OnEndCapture();
            }
        }

        void ExecuteBlitCommands(CommandBuffer commandBuffer)
        {
            foreach (var command in m_BlitCommands)
            {
                GraphicsUtil.Blit(commandBuffer, command);
            }
        }
        
        void OnClusterDisplayUpdate()
        {
            var activeCamera = ClusterCameraManager.Instance.ActiveCamera;
            if (activeCamera == null)
            {
                return;
            }
            // Move early return at the Update's top.
            if (!(m_Settings.GridSize.x > 0 && m_Settings.GridSize.y > 0))
            {
                return;
            }

            // TODO Could remove conditional? Have ClearColor as a setting?
            m_Presenter.ClearColor = m_IsDebug ? m_DebugSettings.BezelColor : Color.black;
            
            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + m_Settings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = m_IsDebug || !ClusterSync.Active ? m_DebugSettings.TileIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
            var numTiles = m_Settings.GridSize.x * m_Settings.GridSize.y;
            var displayMatrixSize = new Vector2Int(m_Settings.GridSize.x * displaySize.x, m_Settings.GridSize.y * displaySize.y);
            
            // Aspect must be updated *before* we pull the projection matrix.
            activeCamera.aspect = displayMatrixSize.x / (float)displayMatrixSize.y;
            var originalProjectionMatrix = activeCamera.projectionMatrix;
            
#if UNITY_EDITOR
            m_Gizmo.TileIndex = currentTileIndex;
            m_Gizmo.GridSize = m_Settings.GridSize;
            m_Gizmo.ViewProjectionInverse = (originalProjectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(m_Settings.GridSize, m_Settings.PhysicalScreenSize, m_Settings.Bezel, m_Settings.OverScanInPixels);
            var blitParams = new BlitParams(displaySize, m_Settings.OverScanInPixels, m_DebugSettings.ScaleBiasTextOffset);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize);

            // Can't turn off asymmetric projection yet.
            var renderFeature = RenderFeature.AsymmetricProjection;

            if (m_DebugSettings.ScreenCoordOverride)
            {
                renderFeature |= RenderFeature.ScreenCoordOverride;
            }

            var renderContext = new RenderContext
            {
                CurrentTileIndex = currentTileIndex,
                NumTiles = numTiles,
                OverscannedSize = overscannedSize,
                Viewport = viewport,
                OriginalProjection = originalProjectionMatrix,
                BlitParams = blitParams,
                PostEffectsParams = postEffectsParams,
                DebugViewportSubsection = m_DebugSettings.ViewportSubsection,
                UseDebugViewportSubsection = m_IsDebug && m_DebugSettings.UseDebugViewportSubsection,
                RenderFeature = renderFeature
            };

            // Allocate tiles targets.
            var isStitcher = m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher;
            var numTargets = isStitcher ? renderContext.NumTiles : 1;
            
            GraphicsUtil.AllocateIfNeeded(ref m_TileRenderTargets, numTargets,  
                renderContext.OverscannedSize.x, 
                renderContext.OverscannedSize.y, m_GraphicsFormat, "Source");

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
            // Screen camera must render *after* all tiles have been rendered.

            // TODO is it really needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        static void RenderStitcher(RenderTexture[] targets, Camera camera, ref RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = CameraScopeFactory.Create(camera, renderContext.RenderFeature);
            
            for (var tileIndex = 0; tileIndex != renderContext.NumTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = renderContext.Viewport.GetSubsectionWithOverscan(tileIndex);

                var asymmetricProjectionMatrix = renderContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);

                var screenSizeOverride = renderContext.PostEffectsParams.GetScreenSizeOverride();
                var screenCoordScaleBias = PostEffectsParams.GetScreenCoordScaleBias(overscannedViewportSubsection);

                cameraScope.Render(asymmetricProjectionMatrix, screenSizeOverride, screenCoordScaleBias, targets[tileIndex]);

                var viewportSubsection = renderContext.Viewport.GetSubsectionWithoutOverscan(tileIndex);

                commands.Add(new BlitCommand(targets[tileIndex], renderContext.BlitParams.ScaleBias, GraphicsUtil.RectAsScaleBias(viewportSubsection)));
            }
        }

        static void RenderTile(RenderTexture target, Camera camera, ref RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = CameraScopeFactory.Create(camera, renderContext.RenderFeature);
            
            var overscannedViewportSubsection = renderContext.UseDebugViewportSubsection ? 
                renderContext.DebugViewportSubsection : 
                renderContext.Viewport.GetSubsectionWithOverscan(renderContext.CurrentTileIndex);
            
            var asymmetricProjectionMatrix = renderContext.OriginalProjection.GetFrustumSlice(overscannedViewportSubsection);
            
            var screenSizeOverride = renderContext.PostEffectsParams.GetScreenSizeOverride();
            var screenCoordScaleBias = PostEffectsParams.GetScreenCoordScaleBias(overscannedViewportSubsection);
            
            cameraScope.Render(asymmetricProjectionMatrix, screenSizeOverride, screenCoordScaleBias, target);

            commands.Add(new BlitCommand(target, renderContext.BlitParams.ScaleBias, GraphicsUtil.RectAsScaleBias(new Rect(0, 0, 1, 1))));
        }
    }
}
