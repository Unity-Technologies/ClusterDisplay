using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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
    [RequireComponent(typeof(ClusterCameraController))]
    public class ClusterRenderer : MonoBehaviour, LayoutBuilder.IClusterRenderer
    {
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

        IXRLayoutBuilder m_TileLayoutBuilder = null;
        IXRLayoutBuilder m_XRStitcherLayoutBuilder = null;
        LayoutBuilder m_LayoutBuilder = null;

        private LayoutMode m_CurrentLayoutMode = LayoutMode.XRTile;
        public LayoutMode CurrentLayoutMode
        {
            get => m_CurrentLayoutMode;
            set
            {
                if (value == m_CurrentLayoutMode)
                    return;

                if (value == LayoutMode.None)
                {
                    m_CurrentLayoutMode = value;
                    SetLayoutBuilder(null);
                    return;
                }

                LayoutBuilder newLayoutBuilder = null;

                switch (value)
                {
                    case LayoutMode.StandardTile:
                        newLayoutBuilder = new StandardTileLayoutBuilder(this);
                        break;

                    case LayoutMode.StandardStitcher:
                        newLayoutBuilder = new StandardStitcherLayoutBuilder(this);
                        break;

                    #if CLUSTER_DISPLAY_XR
                    case LayoutMode.XRTile:
                        newLayoutBuilder = new XRTileLayoutBuilder(this);
                        break;

                    case LayoutMode.XRStitcher:
                        newLayoutBuilder = new XRStitcherLayoutBuilder(this);
                        break;
                    #endif

                    default:
                        throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{value}\".");
                }

                m_CurrentLayoutMode = value;
                SetLayoutBuilder(newLayoutBuilder);
            }
        }

        private ClusterCameraController m_ClusterCameraController;
        public ClusterCameraController CameraController => m_ClusterCameraController;

        [SerializeField]
        ClusterRendererSettings m_Settings;
        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings Settings
        {
            get => m_Settings;
            set => m_Settings = value;
        }
        
        bool m_Debug = false;
        /// <summary>
        /// Enable Debug mode.
        /// </summary>
        public bool Debug
        {
            set { m_Debug = value; }
            get { return m_Debug; }
        }

        ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();
        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings DebugSettings => m_DebugSettings;
       
        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 OriginalProjectionMatrix => m_OriginalProjectionMatrix;

        ClusterRenderContext m_Context = new ClusterRenderContext();
        public ClusterRenderContext Context => m_Context;

        bool m_UsesStitcher;
        
        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";
        static bool s_EnableKeyword = false;
        
#if UNITY_EDITOR
        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        ClusterFrustumGizmo m_Gizmo = new ClusterFrustumGizmo();

        void OnDrawGizmos()
        {
            if (enabled)
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.GridSize, m_Context.TileIndex);
        }

        private void OnValidate() => m_ClusterCameraController = GetComponent<ClusterCameraController>();
#endif

        /// <summary>
        /// Controls activation of Cluster Display specific shader features.
        /// </summary>
        public static bool EnableKeyword
        {
            get => s_EnableKeyword;
            set
            {
                if (value == s_EnableKeyword)
                    return;
                s_EnableKeyword = value;
                if (s_EnableKeyword)
                    Shader.EnableKeyword(k_ShaderKeyword);
                else
                    Shader.DisableKeyword(k_ShaderKeyword);
            }
        }

        void Awake()
        {
            m_TileLayoutBuilder = new XRTileLayoutBuilder(this);
            m_XRStitcherLayoutBuilder = new XRStitcherLayoutBuilder(this);
            m_LayoutBuilder = null;

            m_DebugSettings.Reset();
        }

        void OnEnable()
        {
            if (m_Settings == null)
                m_Settings = new ClusterRendererSettings();
            m_Context.Settings = m_Settings;
            
            m_Context.DebugSettings = m_DebugSettings;
            
            m_UsesStitcher = false;
            EnableKeyword = true;

            CurrentLayoutMode = LayoutMode.XRTile;
        }

        void OnDisable()
        {
            CurrentLayoutMode = LayoutMode.None;
            EnableKeyword = false;
        }

        private void LateUpdate()
        {
            if (m_ClusterCameraController == null)
            {
                m_ClusterCameraController = GetComponent<ClusterCameraController>();
                if (m_ClusterCameraController == null)
                {
                    enabled = false;
                    return;
                }
            }

            m_ClusterCameraController.SetupCameraBeforeRender();
            if (m_ClusterCameraController.CurrentCamera == null || m_ClusterCameraController.CurrentCameraIsSceneViewCamera)
                return;

            // Update aspect ratio
            var camera = m_ClusterCameraController.CurrentCamera;
            camera.aspect = (m_Context.GridSize.x * Screen.width) / (float)(m_Context.GridSize.y * Screen.height);

            m_Context.Debug = m_Debug;
            EnableKeyword = m_DebugSettings.EnableKeyword;
            
            // switch layout builder depending on debug params
            if (m_UsesStitcher != m_DebugSettings.EnableStitcher)
            {
                if (m_DebugSettings.EnableStitcher)
                    CurrentLayoutMode = LayoutMode.XRStitcher;
                else CurrentLayoutMode = LayoutMode.XRTile;

                m_UsesStitcher = m_DebugSettings.EnableStitcher;
            }
            
            // Reset debug viewport subsection
            if (m_Debug && !m_DebugSettings.UseDebugViewportSubsection)
                m_DebugSettings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                    m_Context.GridSize, m_Context.TileIndex);
        }

        void SetLayoutBuilder(LayoutBuilder builder)
        {
            if (m_LayoutBuilder != null)
                m_LayoutBuilder.Dispose();
            m_LayoutBuilder = builder;

            #if CLUSTER_DISPLAY_XR
            if (m_LayoutBuilder is IXRLayoutBuilder)
            {
                if (m_LayoutBuilder == null)
                {
                    XRSystem.SetCustomLayout(null);
                    return;
                }

                XRSystem.SetCustomLayout((m_LayoutBuilder as IXRLayoutBuilder).BuildLayout);
            }

            else
            {

            }
            #else
            #endif
        }

        public void OnBuildLayout(Camera camera)
        {
            #if UNITY_EDITOR
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
            #endif
        }
    }
}