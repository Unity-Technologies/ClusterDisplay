using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Assertions;

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
        LayoutBuilder.ILayoutReceiver, 
        ClusterRendererDebugSettings.IDebugSettingsReceiver
    {
        public delegate void OnPreLateUpdate();
        public delegate void OnPostLateUpdate();
        public delegate void OnEndOfFrame();

        public interface IClusterRendererEventReceiver
        {
            void OnPreLateUpdate();
            void OnPostLateUpdate();
            void OnEndOfFrame();
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

                case LayoutMode.XRTile:
                case LayoutMode.XRStitcher:
                    return true;

                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{layoutMode}\".");
            }
        }

        private OnPreLateUpdate onPreLateUpdate;
        private OnPostLateUpdate onPostLateUpdate;
        private OnEndOfFrame onEndOfFrame;

        private LayoutBuilder m_LayoutBuilder = null;

        [SerializeField] ClusterRendererSettings m_Settings = new ClusterRendererSettings();
        ClusterCameraController m_ClusterCameraController;
        ClusterRendererDebugSettings m_DebugSettings;
        ClusterRenderContext m_Context;

        public ClusterCameraController CameraController => m_ClusterCameraController;

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

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings DebugSettings => m_DebugSettings;
       
        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 OriginalProjectionMatrix => m_OriginalProjectionMatrix;

        public ClusterRenderContext Context => m_Context;

        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";
        
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

        private void RegisterLateUpdateReciever (IClusterRendererEventReceiver clusterRendererEventReceiver)
        {
            onPreLateUpdate += clusterRendererEventReceiver.OnPreLateUpdate;
            onPostLateUpdate += clusterRendererEventReceiver.OnPostLateUpdate;
            onEndOfFrame += clusterRendererEventReceiver.OnEndOfFrame;
        }

        void OnEnable()
        {
            m_ClusterCameraController = new ClusterCameraController();
            RegisterLateUpdateReciever(m_ClusterCameraController);

            m_DebugSettings = new ClusterRendererDebugSettings();
            m_Context = new ClusterRenderContext();

            m_LayoutBuilder = null;
            m_DebugSettings.Reset();
            m_DebugSettings.RegisterDebugSettingsReceiver(this);

            m_Context.Settings = m_Settings;
            m_Context.DebugSettings = m_DebugSettings;

            StartWaitForEndOfFrameCoroutine();
        }

        private void LateUpdate()
        {
            Assert.IsTrue(m_Context.GridSize.x > 0 && m_Context.GridSize.y > 0);

            if (onPreLateUpdate != null)
                onPreLateUpdate();

            if (m_ClusterCameraController.CameraContext != null && !m_ClusterCameraController.CameraContextIsSceneViewCamera)
            {
                // Update aspect ratio
                var camera = m_ClusterCameraController.CameraContext;
                camera.aspect = (m_Context.GridSize.x * Screen.width) / (float)(m_Context.GridSize.y * Screen.height);

                m_Context.Debug = m_Debug;
                
                // Reset debug viewport subsection
                if (m_Debug && !m_DebugSettings.UseDebugViewportSubsection)
                    m_DebugSettings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(
                        m_Context.GridSize, m_Context.TileIndex);

                if (!LayoutModeIsXR(m_DebugSettings.CurrentLayoutMode))
                {
                    var standardLayoutBuilder = m_LayoutBuilder as ILayoutBuilder;
                    standardLayoutBuilder.BuildLayout();
                }
            }

            if (onPostLateUpdate != null)
                onPostLateUpdate();
        }

        private Coroutine m_WaitForEndOfFrameCoroutine;
        private void StartWaitForEndOfFrameCoroutine()
        {
            if (m_WaitForEndOfFrameCoroutine != null)
                StopCoroutine(m_WaitForEndOfFrameCoroutine);
            m_WaitForEndOfFrameCoroutine = StartCoroutine(WaitForEndOfFrameCoroutine());
        }

        private IEnumerator WaitForEndOfFrameCoroutine ()
        {
            while (Application.isPlaying)
            {
                var wait = new WaitForEndOfFrame();
                yield return wait;

                if (onEndOfFrame != null)
                    onEndOfFrame();
            }
        }

        void SetLayoutBuilder(LayoutBuilder builder)
        {
            if (m_LayoutBuilder != null)
                m_LayoutBuilder.Dispose();
            m_LayoutBuilder = builder;

            if (m_LayoutBuilder != null)
            {
                m_LayoutBuilder.RegisterOnReceiveLayout(this);

#if CLUSTER_DISPLAY_XR
                if (LayoutModeIsXR(m_DebugSettings.CurrentLayoutMode))
                    XRSystem.SetCustomLayout((m_LayoutBuilder as IXRLayoutBuilder).BuildLayout);

                else
                {
                    XRSystem.SetCustomLayout(null);
                }
#else
#endif
                return;
            }

            XRSystem.SetCustomLayout(null);
        }

        public void OnBuildLayout(Camera camera)
        {
            #if UNITY_EDITOR
            m_ViewProjectionInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
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
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }

            SetLayoutBuilder(newLayoutBuilder);
        }

        public void OnEnableKeywords(bool keywordsEnabled)
        {
            if (keywordsEnabled)
                Shader.EnableKeyword(k_ShaderKeyword);
            else Shader.DisableKeyword(k_ShaderKeyword);
        }
    }
}