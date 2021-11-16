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

        // TODO Temporary
        [SerializeField]
        bool m_EnableGUI;

        [HideInInspector]
        [SerializeField]
        ClusterRenderContext m_Context = new ClusterRenderContext();

        ILayoutBuilder m_LayoutBuilder;
        Stack<BlitCommand> m_BlitCommands = new Stack<BlitCommand>();

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif
        public bool IsDebug
        {
            get => m_Context.Debug;
            set => m_Context.Debug = value;
        }

        // TODO sketchy, limits client changes for the time being
        internal ClusterRenderContext Context => m_Context;

        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings Settings => m_Context.Settings;

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings debugSettings => m_Context.DebugSettings;

        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        // TODO Is this needed? Users typically manage the projection.
        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 originalProjectionMatrix => m_OriginalProjectionMatrix;


        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        ClusterFrustumGizmo m_Gizmo = new ClusterFrustumGizmo();

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (enabled)
            {
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.GridSize, m_Context.TileIndex);
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
            GraphicsUtil.SetShaderKeyword(m_Context.DebugSettings.EnableKeyword);
            SetLayoutMode(m_Context.DebugSettings.LayoutMode);
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
            if (!(m_Context.GridSize.x > 0 && m_Context.GridSize.y > 0))
            {
                return;
            }

            var activeCamera = ClusterCameraManager.Instance.ActiveCamera;
            if (activeCamera == null)
            {
                return;
            }

            // TODO Could remove conditional?
            m_Presenter.ClearColor = m_Context.Debug ? m_Context.BezelColor : Color.black;
            
            // Context will be removed.
            var settings = m_Context.Settings;
            var debugSettings = m_Context.DebugSettings;
            var debug = m_Context.Debug;
            
            // Aspect must be updated before we pull the projection matrix.
            activeCamera.aspect = LayoutBuilderUtils.GetAspect(m_Context, Screen.width, Screen.height);
            m_OriginalProjectionMatrix = activeCamera.projectionMatrix;
            
            var displaySize = new Vector2Int(Screen.width, Screen.height);
            var overscannedSize = displaySize + settings.OverScanInPixels * 2 * Vector2Int.one;
            var currentTileIndex = (debug || !ClusterSync.Active) ? debugSettings.TileIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;
            var numTiles = settings.GridSize.x * settings.GridSize.y;
            var displayMatrixSize = new Vector2Int(settings.GridSize.x * displaySize.x, settings.GridSize.y * displaySize.y);
            
            // Prepare context properties.
            var viewport = new Viewport(settings.GridSize, settings.PhysicalScreenSize, settings.Bezel, settings.OverScanInPixels);
            var asymmetricProjection = new AsymmetricProjection(m_OriginalProjectionMatrix);
            var blitParams = new BlitParams(displaySize, settings.OverScanInPixels, debugSettings.ScaleBiasTextOffset);
            var postEffectsParams = new PostEffectsParams(displayMatrixSize, settings.GridSize);

            var ctx = new RenderContext
            {
                currentTileIndex = currentTileIndex,
                numTiles = numTiles,
                overscannedSize = overscannedSize,
                displayMatrixSize = displayMatrixSize,
                viewport = viewport,
                asymmetricProjection = asymmetricProjection,
                blitParams = blitParams,
                postEffectsParams = postEffectsParams
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
                // We need to flip along the Y axis when blitting to the camera's backbuffer.
                command.Execute(commandBuffer, true);
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
                    m_LayoutBuilder = new TileLayoutBuilder(m_Context);
                    break;
                case LayoutMode.StandardStitcher:
                    m_LayoutBuilder = new StitcherLayoutBuilder(m_Context);
                    break;
                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }
        }
    }
}
