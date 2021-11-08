using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Unity.ClusterDisplay.Graphics
{
    interface IClusterRendererEventReceiver
    {
        void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
        void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
        void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
        void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
    }

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
        ClusterRendererDebugSettings.IDebugSettingsReceiver
    {
        public enum LayoutMode
        {
            None,
            StandardTile,
            StandardStitcher
        }

        public static bool LayoutModeIsStitcher(LayoutMode layoutMode)
        {
            switch (layoutMode)
            {
                case LayoutMode.None:
                case LayoutMode.StandardTile:
                    return false;
                case LayoutMode.StandardStitcher:
                    return true;
                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{layoutMode}\".");
            }
        }

        // IClusterRendererEventReceiver delegates instances.
        event Action<ScriptableRenderContext, Camera> onBeginCameraRender;
        event Action<ScriptableRenderContext, Camera> onEndCameraRender;
        event Action<ScriptableRenderContext, Camera[]> onBeginFrameRender;
        event Action<ScriptableRenderContext, Camera[]> onEndFrameRender;

        ILayoutBuilder m_LayoutBuilder;

        [HideInInspector]
        [SerializeField]
        ClusterRenderContext m_Context = new ClusterRenderContext();
        
        [HideInInspector]
        [SerializeField] 
        ClusterCameraController m_ClusterCameraController = new ClusterCameraController();

        public bool IsDebug
        {
            get => m_Context.Debug;
            set => m_Context.Debug = value;
        }

        // TODO sketchy, limits client changes for the time being
        internal ClusterRenderContext Context => m_Context;
        
        internal ClusterCameraController CameraController => m_ClusterCameraController;
        
        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings Settings => m_Context.Settings;

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings debugSettings => m_Context.DebugSettings;

        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 originalProjectionMatrix => m_OriginalProjectionMatrix;

        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";

#if UNITY_EDITOR

        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        ClusterFrustumGizmo m_Gizmo = new ClusterFrustumGizmo();

        void OnDrawGizmos()
        {
            if (enabled)
            {
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.GridSize, m_Context.TileIndex);
            }
        }
#endif

        void RegisterRendererEvents(IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onBeginFrameRender += clusterRendererEventReceiver.OnBeginFrameRender;
            onBeginCameraRender += clusterRendererEventReceiver.OnBeginCameraRender;
            onEndCameraRender += clusterRendererEventReceiver.OnEndCameraRender;
            onEndFrameRender += clusterRendererEventReceiver.OnEndFrameRender;
        }

        void UnregisterRendererEvents(IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onBeginFrameRender -= clusterRendererEventReceiver.OnBeginFrameRender;
            onBeginCameraRender -= clusterRendererEventReceiver.OnBeginCameraRender;
            onEndCameraRender -= clusterRendererEventReceiver.OnEndCameraRender;
            onEndFrameRender -= clusterRendererEventReceiver.OnEndFrameRender;
        }

        void OnEnable()
        {
            m_LayoutBuilder = null;

            RegisterRendererEvents(m_ClusterCameraController);
            m_Context.DebugSettings.RegisterDebugSettingsReceiver(this);

            RenderPipelineManager.beginFrameRendering += OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            RenderPipelineManager.endFrameRendering += OnEndFrameRender;

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        void OnDisable()
        {
            UnregisterRendererEvents(m_ClusterCameraController);
            m_Context.DebugSettings.UnRegisterDebugSettingsReceiver(this);

            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;
        }

        void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            onBeginFrameRender?.Invoke(context, cameras);
        }

        void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!CameraContextRegistry.CanChangeContextTo(camera))
            {
                ToggleShaderKeywords(false);
                return;
            }

            ToggleShaderKeywords(debugSettings.EnableKeyword);
            onBeginCameraRender?.Invoke(context, camera);

            Assert.IsTrue(m_Context.GridSize.x > 0 && m_Context.GridSize.y > 0);

            // Update aspect ratio
            camera.aspect = m_Context.GridSize.x * Screen.width / (float)(m_Context.GridSize.y * Screen.height);

            // Reset debug viewport subsection
            if (m_Context.Debug && !m_Context.DebugSettings.UseDebugViewportSubsection)
            {
                m_Context.DebugSettings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                    m_Context.GridSize, m_Context.TileIndex);
            }
        }

        void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!m_ClusterCameraController.CameraIsInContext(camera))
            {
                return;
            }

            onEndCameraRender?.Invoke(context, camera);

#if UNITY_EDITOR
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
#endif
        }

        void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            onEndFrameRender?.Invoke(context, cameras);
        }

        void LateUpdate()
        {
            m_LayoutBuilder?.Update();
        }

        void SetLayoutBuilder(ILayoutBuilder builder)
        {
            if (m_LayoutBuilder != null)
            {
                UnregisterRendererEvents(m_LayoutBuilder);
                m_LayoutBuilder.Dispose();
            }

            m_LayoutBuilder = builder;
            if (m_LayoutBuilder != null)
            {
                RegisterRendererEvents(m_LayoutBuilder);
            }
        }

        public void OnChangeLayoutMode(LayoutMode newLayoutMode)
        {
            if (newLayoutMode == LayoutMode.None)
            {
                SetLayoutBuilder(null);
                return;
            }

            ILayoutBuilder newLayoutBuilder; 
            m_ClusterCameraController.Presenter = new Presenter();

            switch (newLayoutMode)
            {
                case LayoutMode.StandardTile:
                    newLayoutBuilder = new TileLayoutBuilder(this);
                    break;
                case LayoutMode.StandardStitcher:
                    newLayoutBuilder = new StitcherLayoutBuilder(this);
                    break;
                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }

            SetLayoutBuilder(newLayoutBuilder);
        }

        public static void ToggleClusterDisplayShaderKeywords(bool keywordEnabled)
        {
            if (Shader.IsKeywordEnabled(k_ShaderKeyword) == keywordEnabled)
            {
                return;
            }

            if (keywordEnabled)
            {
                Shader.EnableKeyword(k_ShaderKeyword);
            }
            else
            {
                Shader.DisableKeyword(k_ShaderKeyword);
            }
        }

        public void ToggleShaderKeywords(bool keywordEnabled)
        {
            ToggleClusterDisplayShaderKeywords(keywordEnabled);
        }
    }
}
