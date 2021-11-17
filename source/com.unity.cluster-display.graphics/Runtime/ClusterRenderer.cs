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
        struct CameraScope : IDisposable
        {
            public Camera m_Camera;

            public CameraScope(Camera camera)
            {
                m_Camera = camera;
            }

            public void Render(Matrix4x4 projection, Matrix4x4 clusterParams, RenderTexture target)
            {
                m_Camera.targetTexture = target;
                m_Camera.projectionMatrix = projection;
                m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
            
                // TODO Make sure this simple way to pass uniforms is conform to HDRP's expectations.
                // We could have to pass this data through the pipeline.
                Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterParams);

                m_Camera.Render();
            }
            
            public void Dispose()
            {
                m_Camera.ResetAspect();
                m_Camera.ResetProjectionMatrix();
                m_Camera.ResetCullingMatrix();
            }
        }

        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        const string k_LayoutPresentCmdBufferName = "Layout Present";
        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        // We need to flip along the Y axis when blitting to screen on HDRP,
        // but not when using URP.
#if CLUSTER_DISPLAY_HDRP
        const bool k_FlipWhenBlittingToScreen = true;
#else
        const bool k_FlipWhenBlittingToScreen = false;
#endif

        [SerializeField]
        readonly ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        [SerializeField]
        readonly ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();

        readonly List<BlitCommand> m_BlitCommands = new List<BlitCommand>();

        RenderTexture[] m_TileRenderTargets;
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

        // TODO we'll need a method to configure additional camera data for HDRP
        void ____()
        {
            /*if (TryGetPreviousCameraContext(out _))
            {
                additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = m_PreviousAsymmetricProjectionSetting;
                additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, m_PreviousAsymmetricProjectionSetting);
                additionalCameraData.customRenderingSettings = m_PreviousCustomFrameSettingsToggled;
            }

            if (TryGetContextCamera(out var contextCamera) && contextCamera.TryGetComponent(out additionalCameraData))
            {
                m_PreviousAsymmetricProjectionSetting = additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection];
                m_PreviousCustomFrameSettingsToggled = additionalCameraData.customRenderingSettings;

                additionalCameraData.customRenderingSettings = true;
                additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = true;
                additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, true);
                additionalCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
            }*/
        }

        void OnEnable()
        {
            // Sync, will change from inspector as well.
            // TODO We must set the keyword systematically unless in debug mode.
            GraphicsUtil.SetShaderKeyword(m_DebugSettings.EnableKeyword);
            m_Presenter.Enable(gameObject);
            m_Presenter.Present += OnPresent;

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            // TODO Needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        void OnDisable()
        {
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            // TODO Do we assume *one* ClusterRenderer? If not, how do we manage the shader keyword?
            GraphicsUtil.SetShaderKeyword(false);
            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
            
            GraphicsUtil.DeallocateIfNeeded(ref m_TileRenderTargets);
        }
        
        void OnPresent(CommandBuffer commandBuffer)
        {
            foreach (var command in m_BlitCommands)
            {
                command.Execute(commandBuffer, k_FlipWhenBlittingToScreen);
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
            m_Gizmo.tileIndex = currentTileIndex;
            m_Gizmo.gridSize = m_Settings.GridSize;
            m_Gizmo.viewProjectionInverse = (originalProjectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(m_Settings.GridSize, m_Settings.PhysicalScreenSize, m_Settings.Bezel, m_Settings.OverScanInPixels);
            var asymmetricProjection = new AsymmetricProjection(originalProjectionMatrix);
            var blitParams = new BlitParams(displaySize, m_Settings.OverScanInPixels, m_DebugSettings.ScaleBiasTextOffset);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize, m_Settings.GridSize);

            var renderContext = new RenderContext
            {
                currentTileIndex = currentTileIndex,
                numTiles = numTiles,
                overscannedSize = overscannedSize,
                viewport = viewport,
                asymmetricProjection = asymmetricProjection,
                blitParams = blitParams,
                postEffectsParams = postEffectsParams,
                debugViewportSubsection = m_DebugSettings.ViewportSubsection,
                useDebugViewportSubsection = m_IsDebug && m_DebugSettings.UseDebugViewportSubsection
            };

            // Allocate tiles targets.
            var isStitcher = m_DebugSettings.LayoutMode == LayoutMode.StandardStitcher;
            var numTargets = isStitcher ? renderContext.numTiles : 1;
            
            GraphicsUtil.AllocateIfNeeded(ref m_TileRenderTargets, numTargets, "Source", 
                renderContext.overscannedSize.x, 
                renderContext.overscannedSize.y, k_DefaultFormat);

            m_BlitCommands.Clear();
            
            if (isStitcher)
            {
                RenderStitcher(m_TileRenderTargets, activeCamera, renderContext, m_BlitCommands);
            }
            else
            {
                RenderTile(m_TileRenderTargets[0], activeCamera, renderContext, m_BlitCommands);
            }
            
            // TODO Make sure there's no one-frame offset induced by rendering timing.
            // TODO Make sure blitCommands are executed within the frame.
            // Screeen camera must render *after* all tiles have been rendered.

            // TODO is it really needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        static void RenderStitcher(RenderTexture[] targets, Camera camera, RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            for (var tileIndex = 0; tileIndex != renderContext.numTiles; ++tileIndex)
            {
                var overscannedViewportSubsection = renderContext.viewport.GetSubsectionWithOverscan(tileIndex);

                var asymmetricProjectionMatrix = renderContext.asymmetricProjection.GetFrustumSlice(overscannedViewportSubsection);

                var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);

                cameraScope.Render(asymmetricProjectionMatrix, clusterParams, targets[tileIndex]);

                var viewportSubsection = renderContext.viewport.GetSubsectionWithoutOverscan(tileIndex);
                renderContext.blitParams.GetScaleBias(viewportSubsection, out var scaleBiasTex, out var scaleBiasRT);

                commands.Add(new BlitCommand(targets[tileIndex], scaleBiasTex, scaleBiasRT));
            }
        }

        static void RenderTile(RenderTexture target, Camera camera, RenderContext renderContext, List<BlitCommand> commands)
        {
            using var cameraScope = new CameraScope(camera);
            var overscannedViewportSubsection = renderContext.useDebugViewportSubsection ? 
                renderContext.debugViewportSubsection : 
                renderContext.viewport.GetSubsectionWithOverscan(renderContext.currentTileIndex);
            
            var asymmetricProjectionMatrix = renderContext.asymmetricProjection.GetFrustumSlice(overscannedViewportSubsection);

            var clusterParams = renderContext.postEffectsParams.GetAsMatrix4x4(overscannedViewportSubsection);
            
            cameraScope.Render(asymmetricProjectionMatrix, clusterParams, target);

            renderContext.blitParams.GetScaleBias(new Rect(0, 0, 1, 1), out var scaleBiasTex, out var scaleBiasRT);

            commands.Add(new BlitCommand(target, scaleBiasTex, scaleBiasRT));
        }

        internal static void SetShaderKeyword(bool enabled)
        {
            if (Shader.IsKeywordEnabled(k_ShaderKeyword) == enabled)
            {
                return;
            }

            if (enabled)
            {
                Shader.EnableKeyword(k_ShaderKeyword);
            }
            else
            {
                Shader.DisableKeyword(k_ShaderKeyword);
            }
        }
    }
}
