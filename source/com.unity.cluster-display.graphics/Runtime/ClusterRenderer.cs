using System;
using System.Linq;
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
    public class ClusterRenderer : SingletonScriptableObject<ClusterRenderer>
    {
        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        bool m_IsDebug;

        [SerializeField]
        ProjectionPolicy m_ProjectionPolicy;

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif

        internal const int VirtualObjectLayer = 12;

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
        public ClusterRendererSettings Settings => m_Settings;
        
        private T CreateProjectionPolicyAsset<T>(string folder) where T : ProjectionPolicy, new()
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
        public void SetProjectionPolicy<T>() where T : ProjectionPolicy
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
            m_Presenter.Enable();
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

            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
        }

        void OnPresent(CommandBuffer commandBuffer)
        {
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy.Present(commandBuffer);
            }
        }

        void OnClusterDisplayUpdate()
        {
            if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
                return;
            
            var activeCamera = ClusterCameraManager.ActiveCamera;
            if (activeCamera == null || m_ProjectionPolicy == null)
            {
                return;
            }

            ClusterDebug.Log($"Starting render.");
            m_ProjectionPolicy.UpdateCluster(m_Settings, activeCamera);
        }
    }
}
