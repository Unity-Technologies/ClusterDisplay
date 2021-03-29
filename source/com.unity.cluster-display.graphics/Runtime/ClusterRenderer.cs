using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// This component is responsible for managing projection, layout (tile, stitcher),
    /// and Cluster Display specific shader features such as Global Screen Space.
    /// </summary>
    /// <remarks>
    /// We typically expect at most one instance active at a given time.
    /// </remarks>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class ClusterRenderer : MonoBehaviour
    {
        // ILayoutBuilder m_TileLayoutBuilder = new TileLayoutBuilder();
        // ILayoutBuilder m_StitcherLayoutBuilder = new StitcherLayoutBuilder();
        // ILayoutBuilder m_LayoutBuilder = null;
        
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
        public Matrix4x4 OriginalProjectionMatrix
        {
            get { return m_OriginalProjectionMatrix; }
        }
        
        ClusterRenderContext m_Context = new ClusterRenderContext();
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
            
            // SetLayoutBuilder(m_TileLayoutBuilder);
            // XRSystem.SetCustomLayout(BuildLayout);
        }

        void OnDisable()
        {
            // XRSystem.SetCustomLayout(null);
            EnableKeyword = false;
            // m_LayoutBuilder.Dispose();
            // m_LayoutBuilder = null;
        }

        void Update()
        {
            // Update aspect ratio
            var camera = Camera.main;
            if (camera != null)
                camera.aspect = (m_Context.GridSize.x * Screen.width) / (float)(m_Context.GridSize.y * Screen.height);

            m_Context.Debug = m_Debug;

            EnableKeyword = m_DebugSettings.EnableKeyword;
            
            // switch layout builder depending on debug params
            if (m_UsesStitcher != m_DebugSettings.EnableStitcher)
            {
                /*
                if (m_DebugSettings.EnableStitcher)
                    SetLayoutBuilder(m_StitcherLayoutBuilder);
                else 
                    SetLayoutBuilder(m_TileLayoutBuilder);
                */
                UnityEngine.Debug.LogError("Un-implemented stitcher!");
                m_UsesStitcher = m_DebugSettings.EnableStitcher;
            }
            
            // Reset debug viewport subsection
            if (m_Debug && !m_DebugSettings.UseDebugViewportSubsection)
                m_DebugSettings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                    m_Context.GridSize, m_Context.TileIndex);
        }

        /*
        void SetLayoutBuilder(ILayoutBuilder builder)
        {
            if (m_LayoutBuilder != null)
                m_LayoutBuilder.Dispose();
            
            m_LayoutBuilder = builder;
            
            if (m_LayoutBuilder != null)
                m_LayoutBuilder.Initialize();
        }
        */

        /*
        bool BuildLayout(XRLayout layout)
        {
            var camera = layout.camera;
            if (!(camera != null && camera.cameraType == CameraType.Game && camera.TryGetCullingParameters(false, out var cullingParams)))
                return false;

#if UNITY_EDITOR
            // update matrix used for drawing gizmos
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
#endif
            return m_LayoutBuilder.BuildLayout(layout, m_Context, cullingParams);
        }
        */
    }
}