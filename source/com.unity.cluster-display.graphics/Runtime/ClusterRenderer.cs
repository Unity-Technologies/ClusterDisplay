using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
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
        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        const string k_LayoutPresentCmdBufferName = "Layout Present";

        // We need to flip along the Y axis when blitting to screen on HDRP,
        // but not when using URP.
#if CLUSTER_DISPLAY_HDRP
        const bool k_FlipWhenBlittingToScreen = true;
#else
        const bool k_FlipWhenBlittingToScreen = false;
#endif

        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        [SerializeField]
        ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();

        ILayoutBuilder m_LayoutBuilder;
        Stack<BlitCommand> m_BlitCommands = new Stack<BlitCommand>();
        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;
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

        // TODO Is this needed? Users typically manage the projection.
        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 originalProjectionMatrix => m_OriginalProjectionMatrix;


        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        SlicedFrustumGizmo m_Gizmo = new SlicedFrustumGizmo();

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
            SetLayoutMode(m_DebugSettings.LayoutMode);
            m_Presenter.Enable(gameObject);
            m_Presenter.Present += OnPresent;

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(InjectedUpdate);
        }

        void OnDisable()
        {
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(InjectedUpdate);

            // TODO We assume ONE ClusterRenderer. Enforce it.
            GraphicsUtil.SetShaderKeyword(false);
            SetLayoutMode(LayoutMode.None);
            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
        }

        void InjectedUpdate()
        {
            // Move early return at the Update's top.
            if (!(m_Settings.GridSize.x > 0 && m_Settings.GridSize.y > 0))
            {
                return;
            }

            var activeCamera = ClusterCameraManager.Instance.ActiveCamera;
            if (activeCamera == null)
            {
                return;
            }

            // TODO Could remove conditional?
            m_Presenter.ClearColor = m_IsDebug ? m_DebugSettings.BezelColor : Color.black;
            
            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + m_Settings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = (m_IsDebug || !ClusterSync.Active) ? m_DebugSettings.TileIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
            var numTiles = m_Settings.GridSize.x * m_Settings.GridSize.y;
            var displayMatrixSize = new Vector2Int(m_Settings.GridSize.x * displaySize.x, m_Settings.GridSize.y * displaySize.y);
            
            // Aspect must be updated *before* we pull the projection matrix.
            m_Camera.aspect = displayMatrixSize.x / (float)displayMatrixSize.y;
            m_OriginalProjectionMatrix = m_Camera.projectionMatrix;
            
            
#if UNITY_EDITOR
            m_Gizmo.tileIndex = currentTileIndex;
            m_Gizmo.gridSize = m_Settings.GridSize;
            m_Gizmo.viewProjectionInverse = (originalProjectionMatrix * m_Camera.worldToCameraMatrix).inverse;
#endif

            // Prepare context properties.
            var viewport = new Viewport(m_Settings.GridSize, m_Settings.PhysicalScreenSize, m_Settings.Bezel, m_Settings.OverScanInPixels);
            var asymmetricProjection = new AsymmetricProjection(m_OriginalProjectionMatrix);
            var blitParams = new BlitParams(displaySize, m_Settings.OverScanInPixels, m_DebugSettings.ScaleBiasTextOffset);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize, m_Settings.GridSize);

            var ctx = new RenderContext
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
            
            TryRenderLayout(ctx);

            // TODO Make sur there's no one-frame offset induced by rendering timing.
            TryPresentLayout(ctx);
            
            
            // TODO is it really needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        // TODO temporary functions, while we figure the right time to invoke it.
        void TryRenderLayout(RenderContext renderContext)
        {
            
#if UNITY_EDITOR
            m_ViewProjectionInverse = (activeCamera.projectionMatrix * activeCamera.worldToCameraMatrix).inverse;
#endif

            // TODO consider a null-object pattern for layout. It is *not* expected to be null while the cluster-renderer is enabled.
            m_LayoutBuilder?.Render(activeCamera, renderContext);
        }

        void TryPresentLayout(RenderContext renderContext)
        {
            // TODO use null-object.
            if (m_LayoutBuilder != null)
            {
                // TODO Check that the stack of commands has been consumed?
                m_BlitCommands.Clear();
                foreach (var command in m_LayoutBuilder.Present(renderContext))
                {
                    m_BlitCommands.Push(command);
                }
            }
        }

        void OnPresent(CommandBuffer commandBuffer)
        {
            while (m_BlitCommands.Count > 0)
            {
                var command = m_BlitCommands.Pop();
                command.Execute(commandBuffer, k_FlipWhenBlittingToScreen);
            }
        }

        internal void SetLayoutMode(LayoutMode newLayoutMode)
        {
            if (m_LayoutBuilder != null && m_LayoutBuilder.LayoutMode == newLayoutMode)
            {
                return;
            }

            if (m_LayoutBuilder != null)
            {
                m_LayoutBuilder.Dispose();
                m_LayoutBuilder = null;
            }

            switch (newLayoutMode)
            {
                case LayoutMode.None:
                    m_LayoutBuilder = null;
                    break;
                case LayoutMode.StandardTile:
                    m_LayoutBuilder = new TileLayoutBuilder();
                    break;
                case LayoutMode.StandardStitcher:
                    m_LayoutBuilder = new StitcherLayoutBuilder();
                    break;
                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }
        }
    }
}
