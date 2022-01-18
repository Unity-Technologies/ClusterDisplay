using System;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Manages Cluster Display rendering.
    /// </summary>
    /// <remarks>
    /// Instantiate this component to render using cluster-specific projections.
    /// </remarks>
    [CreateAssetMenu(fileName = "ClusterRenderer", menuName = "Cluster Display/ClusterRenderer", order = 1)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public class ClusterRenderer : SingletonScriptableObject<ClusterRenderer>
    {
        static ClusterRenderer() => Initialize();
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            ClusterDisplayManager.onEnable -= OnEnabled;
            ClusterDisplayManager.onDisable -= OnDisabled;
            
            ClusterDisplayManager.onEnable += OnEnabled;
            ClusterDisplayManager.onDisable += OnDisabled;
        }

        private static void OnEnabled()
        {
            if (ClusterRenderer.TryGetInstance(out var clusterRenderer))
                clusterRenderer.OnEnable();
        }
        
        private static void OnDisabled()
        {
            if (ClusterRenderer.TryGetInstance(out var clusterRenderer))
                clusterRenderer.OnDisable();
        }
        
        // Temporary, we need a way to *not* procedurally deactivate cameras when no cluster rendering occurs.
        static int s_ActiveInstancesCount;
        internal static bool IsActive() => s_ActiveInstancesCount > 0;

        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        bool m_IsDebug;

        [SerializeField]
        ProjectionPolicy m_ProjectionPolicy;

        public delegate void PreRenderCameraDataOverride(
            int nodeId, 
            ref Vector3 position, 
            ref Quaternion rotation, 
            ref Matrix4x4 projectionMatrix);
        
        public PreRenderCameraDataOverride preRenderCameraDataOverride;

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif

        internal const int VirtualObjectLayer = 12;
        
		const string k_IconName = "BuildSettings.Metro On@2x";

        internal ProjectionPolicy ProjectionPolicy => m_ProjectionPolicy;

        /// <summary>
        /// Enable debug mode.
        /// </summary>
        public bool IsDebug
        {
            get => m_IsDebug;
            set => m_IsDebug = value;
        }

        /// <summary>
        /// Gets the current cluster rendering settings.
        /// </summary>
        public ClusterRendererSettings Settings
        {
            get => m_Settings;
            set => m_Settings = value;
        }
		
		/// <summary>
        /// Gets the camera internally used to present on screen.
        /// </summary>
		public Camera PresentCamera => m_Presenter.Camera;

        internal T CreateProjectionPolicyAsset<T>(string folder) where T : ProjectionPolicy, new()
        {
            var newProjectionPolicy = ScriptableObject.CreateInstance<T>();
            #if UNITY_EDITOR
            AssetDatabase.CreateAsset(newProjectionPolicy, $"{folder}/{typeof(T).Name}.asset");
            #endif
            return newProjectionPolicy;
        }

        /// <summary>
        /// Set the current projection policy.
        /// </summary>
        /// <typeparam name="T">The projection policy type to set.</typeparam>
        /// <remarks>
        /// The projection policy determines how the content is to be rendered. Only one policy
        /// can be active at a time, so calling this method multiple times will override any
        /// previously-active policies (and potentially erase all of the previous settings).
        /// </remarks>
        internal void SetProjectionPolicy<T>() where T : ProjectionPolicy
        {
            if (m_ProjectionPolicy != null && m_ProjectionPolicy.GetType() == typeof(T))
                return;
            
            var resourcePath = $"{typeof(T).Name}";
            var projectionPolicy = Resources.Load<T>(resourcePath);
            if (projectionPolicy == null)
            {
                projectionPolicy = ScriptableObject.CreateInstance<T>();
                #if UNITY_EDITOR
                var assetPathOfClusterRenderer = AssetDatabase.GetAssetPath(this);
                var folder = Path.GetDirectoryName(assetPathOfClusterRenderer);
                AssetDatabase.CreateAsset(projectionPolicy, $"{folder}/{typeof(T).Name}.asset");
                #endif
            }

            m_ProjectionPolicy = projectionPolicy;
        }

        void OnDestroy()
        {
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy = null;
            }
        }

        void OnEnable()
        {
            // TODO Keyword should be set for one render only at a time. Ex: not when rendering the scene camera.
            // EnableScreenCoordOverrideKeyword(m_DebugSettings.EnableKeyword);
            m_Presenter.Enable();
            m_Presenter.Present += OnPresent;
            m_ProjectionPolicy.OnEnable();

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            // TODO Needed?
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        void OnDisable()
        {
            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            --s_ActiveInstancesCount;
            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
            
            m_ProjectionPolicy.OnDisable();
        }

        private bool ShouldRender
        {
            get
            {
                if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
                {
                    return false;
                }
                
                var activeCamera = ClusterCameraManager.ActiveCamera;
                if (activeCamera == null || m_ProjectionPolicy == null)
                {
                    return false;
                }

                return true;
            }
        }

         void OnPresent(PresentArgs args)
        {
            if (!ShouldRender)
            {
                return;
            }
            
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy.Present(args);
            }
        }

        void OnClusterDisplayUpdate()
        {
            if (!ShouldRender)
            {
                return;
            }
            
            var activeCamera = ClusterCameraManager.ActiveCamera;
            ClusterDebug.Log($"Starting render.");
            m_ProjectionPolicy.UpdateCluster(
                preRenderCameraDataOverride, 
                m_Settings, 
                activeCamera);
        }
    }
}
