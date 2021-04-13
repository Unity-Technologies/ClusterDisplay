using System;
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
    public class ClusterRenderer : 
        MonoBehaviour, 
        IClusterRenderer, 
        ClusterRendererDebugSettings.IDebugSettingsReceiver
    {
        public delegate void OnSetup();
        public delegate void OnTearDown();
        public delegate void OnBeginCameraRenderDelegate(ScriptableRenderContext context, Camera camera);
        public delegate void OnEndCameraRenderDelegate(ScriptableRenderContext context, Camera camera);
        public delegate void OnBeginFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);
        public delegate void OnEndFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);

        public interface IClusterRendererEventReceiver
        {
            void OnSetup();
            void OnTearDown();
            void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
            void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
            void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
            void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
        }

        public enum LayoutMode
        {
            None,

            StandardTile,
            StandardStitcher,

#if CLUSTER_DISPLAY_XR
            XRTile,
            XRStitcher
#endif
        }

        public bool LayoutModeIsXR (LayoutMode layoutMode)
        {
            switch (layoutMode)
            {
                case LayoutMode.None:
                case LayoutMode.StandardTile:
                case LayoutMode.StandardStitcher:
                    return false;

#if CLUSTER_DISPLAY_XR
                case LayoutMode.XRTile:
                case LayoutMode.XRStitcher:
                    return true;
#endif

                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{layoutMode}\".");
            }
        }

        private OnSetup onSetup;
        private OnTearDown onTearDown;

        private OnBeginCameraRenderDelegate onBeginCameraRender;
        private OnEndCameraRenderDelegate onEndCameraRender;

        private OnBeginFrameRenderDelegate onBeginFrameRender;
        private OnEndFrameRenderDelegate onEndFrameRender;

        public long frameIndex = 0;

        private LayoutBuilder m_LayoutBuilder = null;

        [HideInInspector][SerializeField] private ClusterRenderContext m_Context = new ClusterRenderContext();
#if CLUSTER_DISPLAY_HDRP
        [HideInInspector][SerializeField] private ClusterCameraController m_ClusterCameraController = new HDRPClusterCameraController();
#else
        [HideInInspector][SerializeField] private ClusterCameraController m_ClusterCameraController = new ClusterCameraController();
#endif

        public ClusterCameraController CameraController => m_ClusterCameraController;

        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings Settings => m_Context.Settings;

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings DebugSettings => m_Context.DebugSettings;
       
        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 OriginalProjectionMatrix => m_OriginalProjectionMatrix;

        public ClusterRenderContext Context => m_Context;

        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";
        private bool m_ShaderKeywordState = false;
        
#if UNITY_EDITOR
        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        private ClusterFrustumGizmo m_Gizmo = new ClusterFrustumGizmo();

        void OnDrawGizmos()
        {
            if (enabled)
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.GridSize, m_Context.TileIndex);
        }
#endif

        private void RegisterRendererEvents (IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onBeginFrameRender += clusterRendererEventReceiver.OnBeginFrameRender;
            onBeginCameraRender += clusterRendererEventReceiver.OnBeginCameraRender;
            onEndCameraRender += clusterRendererEventReceiver.OnEndCameraRender;
            onEndFrameRender += clusterRendererEventReceiver.OnEndFrameRender;
            onSetup += clusterRendererEventReceiver.OnSetup;
            onTearDown += clusterRendererEventReceiver.OnTearDown;
        }

        private void UnRegisterLateUpdateReciever (IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onBeginFrameRender -= clusterRendererEventReceiver.OnBeginFrameRender;
            onBeginCameraRender -= clusterRendererEventReceiver.OnBeginCameraRender;
            onEndCameraRender -= clusterRendererEventReceiver.OnEndCameraRender;
            onEndFrameRender -= clusterRendererEventReceiver.OnEndFrameRender;

            onSetup -= clusterRendererEventReceiver.OnSetup;
            onTearDown -= clusterRendererEventReceiver.OnTearDown;
        }

        private bool m_Setup = false;
        private void Setup ()
        {
            if (m_Setup)
                return;

            m_LayoutBuilder = null;

            RegisterRendererEvents(m_ClusterCameraController);
            m_Context.DebugSettings.RegisterDebugSettingsReceiver(this);

            RenderPipelineManager.beginFrameRendering += OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            RenderPipelineManager.endFrameRendering += OnEndFrameRender;

            RTHandles.Initialize(Screen.width, Screen.height, true, MSAASamples.MSAA8x);

            if (onSetup != null)
                onSetup();

            m_Setup = true;
        }

        private void TearDown ()
        {
            if (!m_Setup)
                return;

            UnRegisterLateUpdateReciever(m_ClusterCameraController);
            m_Context.DebugSettings.UnRegisterDebugSettingsReceiver(this);

            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;

            if (onTearDown != null)
                onTearDown();

            m_Setup = false;
        }

        private void Awake() => Setup();
        private void OnEnable() => Setup();
        private void OnDisable() => TearDown();
        private void OnDestroy() => TearDown();

        private void OnBeginFrameRender (ScriptableRenderContext context, Camera[] cameras) {}
        private void OnBeginCameraRender (ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
            {
                ToggleClusterDisplayShaderKeywords(keywordEnabled: false);
                return;
            }

            ToggleClusterDisplayShaderKeywords(keywordEnabled: m_ShaderKeywordState);
            onBeginCameraRender(context, camera);

            Assert.IsTrue(m_Context.GridSize.x > 0 && m_Context.GridSize.y > 0);

            // Update aspect ratio
            camera.aspect = (m_Context.GridSize.x * Screen.width) / (float)(m_Context.GridSize.y * Screen.height);

            // Reset debug viewport subsection
            if (m_Context.Debug && !m_Context.DebugSettings.UseDebugViewportSubsection)
                m_Context.DebugSettings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                    m_Context.GridSize, m_Context.TileIndex);
        }

        private void OnEndCameraRender (ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterCameraController.CameraContext)
                return;

            if (onEndCameraRender != null)
                onEndCameraRender(context, camera);

            frameIndex++;
#if UNITY_EDITOR
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
#endif
        }

        private void OnEndFrameRender (ScriptableRenderContext context, Camera[] cameras) {}

        private void LateUpdate()
        {
            if (m_LayoutBuilder == null)
                return;

            m_LayoutBuilder.LateUpdate();
        }

        void SetLayoutBuilder(LayoutBuilder builder)
        {
            if (m_LayoutBuilder != null)
            {
                UnRegisterLateUpdateReciever(m_LayoutBuilder);
                m_LayoutBuilder.Dispose();
            }

            m_LayoutBuilder = builder;
            if (m_LayoutBuilder != null)
            {
                RegisterRendererEvents(m_LayoutBuilder);
#if !CLUSTER_DISPLAY_XR
            }
#else
                if (LayoutModeIsXR(m_Context.DebugSettings.CurrentLayoutMode))
                    XRSystem.SetCustomLayout((m_LayoutBuilder as IXRLayoutBuilder).BuildLayout);
                else XRSystem.SetCustomLayout(null);
                return;
            }

            XRSystem.SetCustomLayout(null);
#endif
        }

        public void OnChangeLayoutMode(LayoutMode newLayoutMode)
        {
            if (newLayoutMode == LayoutMode.None)
            {
                SetLayoutBuilder(null);
                return;
            }

            LayoutBuilder newLayoutBuilder = null;

            switch (newLayoutMode)
            {
                case LayoutMode.StandardTile:
                    newLayoutBuilder = new StandardTileLayoutBuilder(this);
                    CameraController.Presenter = new StandardHDRPPresenter();
                    break;

                case LayoutMode.StandardStitcher:
                    newLayoutBuilder = new StandardStitcherLayoutBuilder(this);
                    CameraController.Presenter = new StandardHDRPPresenter();
                    break;

#if CLUSTER_DISPLAY_XR
                case LayoutMode.XRTile:
                    newLayoutBuilder = new XRTileLayoutBuilder(this);
                    CameraController.Presenter = new XRHHDRPPresenter();
                    break;

                case LayoutMode.XRStitcher:
                    newLayoutBuilder = new XRStitcherLayoutBuilder(this);
                    CameraController.Presenter = new XRHHDRPPresenter();
                    break;
#endif

                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }

            SetLayoutBuilder(newLayoutBuilder);
        }

        public void ToggleClusterDisplayShaderKeywords(bool keywordEnabled)
        {
            if (keywordEnabled == m_ShaderKeywordState)
                return;

            m_ShaderKeywordState = keywordEnabled;

            if (keywordEnabled)
                Shader.EnableKeyword(k_ShaderKeyword);
            else Shader.DisableKeyword(k_ShaderKeyword);
        }
    }
}