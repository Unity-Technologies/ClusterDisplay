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

    interface IClusterRendererModule
    {
        void OnSetCustomLayout(LayoutBuilder layoutBuilder);
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
        IClusterRenderer,
        ClusterRendererDebugSettings.IDebugSettingsReceiver
    {
        // |---> IClusterRendererEventReceiver delegates for RenderPipelineManager.*
        public delegate void OnBeginCameraRenderDelegate(ScriptableRenderContext context, Camera camera);

        public delegate void OnEndCameraRenderDelegate(ScriptableRenderContext context, Camera camera);

        public delegate void OnBeginFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);

        public delegate void OnEndFrameRenderDelegate(ScriptableRenderContext context, Camera[] cameras);

        // <---|

        // |---> IClusterRendererModule delegates.
        delegate void OnSetCustomLayout(LayoutBuilder layoutBuilder);

        // <---|

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

        public static bool LayoutModeIsXR(LayoutMode layoutMode)
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

        public static bool LayoutModeIsStitcher(LayoutMode layoutMode)
        {
            switch (layoutMode)
            {
                case LayoutMode.None:
                case LayoutMode.StandardTile:
#if CLUSTER_DISPLAY_XR
                case LayoutMode.XRTile:
#endif
                    return false;
                case LayoutMode.StandardStitcher:
#if CLUSTER_DISPLAY_XR
                case LayoutMode.XRStitcher:
#endif
                    return true;

                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{layoutMode}\".");
            }
        }

        // |---> IClusterRendererEventReceiver delegates instances.
        OnBeginCameraRenderDelegate onBeginCameraRender;
        OnEndCameraRenderDelegate onEndCameraRender;
        OnBeginFrameRenderDelegate onBeginFrameRender;
        OnEndFrameRenderDelegate onEndFrameRender;

        // <---|

        // |---> IClusterRendererModule delgate instances.
        OnSetCustomLayout m_OnSetCustomLayout;

        // <---|

        LayoutBuilder m_LayoutBuilder;

        [HideInInspector]
        [SerializeField]
        ClusterRenderContext m_Context = new ClusterRenderContext();
#if CLUSTER_DISPLAY_HDRP
        [HideInInspector]
        [SerializeField]
        ClusterCameraController m_ClusterCameraController = new HdrpClusterCameraController();
        [HideInInspector]
        [SerializeField]
        IClusterRendererModule m_ClusterRendererModule = new HDRPClusterRendererModule();
#else
        [HideInInspector][SerializeField] ClusterCameraController m_ClusterCameraController = new URPClusterCameraController();
        [HideInInspector][SerializeField] IClusterRendererModule m_ClusterRendererModule = new URPClusterRendererModule();
#endif

        public bool IsDebug
        {
            get => m_Context.debug;
            set => m_Context.debug = value;
        }

        // TODO sketchy, limits client changes for the time being
        internal ClusterRenderContext context => m_Context;
        ClusterRenderContext IClusterRenderer.context => m_Context;

        internal ClusterCameraController cameraController => m_ClusterCameraController;
        ClusterCameraController IClusterRenderer.cameraController => m_ClusterCameraController;

        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings settings => m_Context.settings;

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings debugSettings => m_Context.debugSettings;

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
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.gridSize, m_Context.tileIndex);
        }

        void OnValidate()
        {
            if (settings.resources == null)
            {
                var assets = AssetDatabase.FindAssets($"t:{nameof(ClusterDisplayResources)}");
                if (assets.Length == 0)
                    throw new Exception($"No valid instances of: {nameof(ClusterDisplayResources)} exist in the project.");
                settings.resources = AssetDatabase.LoadAssetAtPath<ClusterDisplayResources>(AssetDatabase.GUIDToAssetPath(assets[0]));
                Debug.Log($"Applied instance of: {nameof(ClusterDisplayResources)} named: \"{settings.resources.name}\" to cluster display settings.");
                EditorUtility.SetDirty(this);
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

        void UnRegisterLateUpdateReciever(IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onBeginFrameRender -= clusterRendererEventReceiver.OnBeginFrameRender;
            onBeginCameraRender -= clusterRendererEventReceiver.OnBeginCameraRender;
            onEndCameraRender -= clusterRendererEventReceiver.OnEndCameraRender;
            onEndFrameRender -= clusterRendererEventReceiver.OnEndFrameRender;
        }

        void RegisterModule(IClusterRendererModule module)
        {
            m_OnSetCustomLayout += module.OnSetCustomLayout;
        }

        void UnRegisterModule(IClusterRendererModule module)
        {
            m_OnSetCustomLayout -= module.OnSetCustomLayout;
        }

        void OnEnable()
        {
            m_LayoutBuilder = null;

            RegisterRendererEvents(m_ClusterCameraController);
            RegisterModule(m_ClusterRendererModule);
            m_Context.debugSettings.RegisterDebugSettingsReceiver(this);

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
            UnRegisterLateUpdateReciever(m_ClusterCameraController);
            UnRegisterModule(m_ClusterRendererModule);
            m_Context.debugSettings.UnRegisterDebugSettingsReceiver(this);

            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;
        }

        void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            if (onBeginFrameRender != null)
                onBeginFrameRender(context, cameras);
        }

        void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!CameraContextRegistry.CanChangeContextTo(camera))
            {
                ToggleShaderKeywords(false);
                return;
            }

            ToggleShaderKeywords(debugSettings.enableKeyword);
            onBeginCameraRender(context, camera);

            Assert.IsTrue(m_Context.gridSize.x > 0 && m_Context.gridSize.y > 0);

            // Update aspect ratio
            camera.aspect = m_Context.gridSize.x * Screen.width / (float)(m_Context.gridSize.y * Screen.height);

            // Reset debug viewport subsection
            if (m_Context.debug && !m_Context.debugSettings.useDebugViewportSubsection)
                m_Context.debugSettings.viewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                    m_Context.gridSize, m_Context.tileIndex);
        }

        void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!m_ClusterCameraController.CameraIsInContext(camera))
                return;

            if (onEndCameraRender != null)
                onEndCameraRender(context, camera);

#if UNITY_EDITOR
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
#endif
        }

        void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            if (onEndFrameRender != null)
                onEndFrameRender(context, cameras);
        }

        void LateUpdate()
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
                RegisterRendererEvents(m_LayoutBuilder);

            if (m_OnSetCustomLayout != null)
                m_OnSetCustomLayout(m_LayoutBuilder);
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
#if CLUSTER_DISPLAY_HDRP
                    newLayoutBuilder = new HdrpStandardTileLayoutBuilder(this);
#else
                    newLayoutBuilder = new URPStandardTileLayoutBuilder(this);
#endif
                    m_ClusterCameraController.presenter = new StandardPresenter();
                    break;

                case LayoutMode.StandardStitcher:
#if CLUSTER_DISPLAY_HDRP
                    newLayoutBuilder = new HdrpStandardStitcherLayoutBuilder(this);
#else
                    newLayoutBuilder = new URPStandardStitcherLayoutBuilder(this);
#endif
                    m_ClusterCameraController.presenter = new StandardPresenter();
                    break;

#if CLUSTER_DISPLAY_XR
                case LayoutMode.XRTile:
                    newLayoutBuilder = new XRTileLayoutBuilder(this);
                    m_ClusterCameraController.presenter = new XRPresenter();
                    break;

                case LayoutMode.XRStitcher:
                    newLayoutBuilder = new XRStitcherLayoutBuilder(this);
                    m_ClusterCameraController.presenter = new XRPresenter();
                    break;
#endif

                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }

            SetLayoutBuilder(newLayoutBuilder);
        }

        public static void ToggleClusterDisplayShaderKeywords(bool keywordEnabled)
        {
            if (Shader.IsKeywordEnabled(k_ShaderKeyword) == keywordEnabled) return;

            if (keywordEnabled)
                Shader.EnableKeyword(k_ShaderKeyword);
            else
                Shader.DisableKeyword(k_ShaderKeyword);
        }

        public void ToggleShaderKeywords(bool keywordEnabled)
        {
            ToggleClusterDisplayShaderKeywords(keywordEnabled);
        }
    }
}
