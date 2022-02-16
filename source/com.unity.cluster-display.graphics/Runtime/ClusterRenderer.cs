using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
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
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterRenderer : SingletonMonoBehaviour<ClusterRenderer>
    {
        // Temporary, we need a way to *not* procedurally deactivate cameras when no cluster rendering occurs.
        static int s_ActiveInstancesCount;
        internal static bool IsActive() => s_ActiveInstancesCount > 0;
        internal static Action Enabled = delegate { };
        internal static Action Disabled = delegate { };

        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        [SerializeField]
        ProjectionPolicy m_ProjectionPolicy;

        [SerializeField]
        bool m_DelayPresentByOneFrame;

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif

        internal const int VirtualObjectLayer = 12;

        // TODO: Create a custom icon.
        const string k_IconName = "BuildSettings.Metro On@2x";

        internal ProjectionPolicy ProjectionPolicy => m_ProjectionPolicy;

        /// <summary>
        /// If true, buffers and delays the presentation of the camera output by one frame.
        /// </summary>
        internal bool DelayPresentByOneFrame
        {
            get => m_DelayPresentByOneFrame;
            set
            {
                m_DelayPresentByOneFrame = value;
                m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
            }
        }

        /// <summary>
        /// Specifies whether the current projection policy is in debug mode.
        /// </summary>
        internal bool IsDebug
        {
            set
            {
                if (m_ProjectionPolicy != null)
                {
                    m_ProjectionPolicy.IsDebug = value;
                }
            }
            get => m_ProjectionPolicy != null && m_ProjectionPolicy.IsDebug;
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

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, k_IconName);
            if (m_ProjectionPolicy != null && Selection.Contains(gameObject))
            {
                m_ProjectionPolicy.OnDrawGizmos();
            }
        }
#endif

        // TODO Why do we need to "delete" the base implementation?
        protected override void OnAwake()
        {
        }

        void OnValidate()
        {
            m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
        }

        void OnEnable()
        {
            ++s_ActiveInstancesCount;

            // TODO More elegant / user friendly way to handle this.
            // If we stick to inheriting SingletonMonoBehaviour, this may be removed.
            if (s_ActiveInstancesCount > 1)
            {
                throw new InvalidOperationException($"At most one instance of {nameof(ClusterRenderer)} can be active.");
            }

            m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
            m_Presenter.Enable(gameObject);
            m_Presenter.Present += OnPresent;

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(OnClusterDisplayUpdate);

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
            Enabled.Invoke();
        }

        void Update()
        {
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy.Origin = transform.localToWorldMatrix;
            }
        }

        void OnDisable()
        {
            Disabled.Invoke();

            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            --s_ActiveInstancesCount;
            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();
        }

        void OnPresent(PresentArgs args)
        {
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy.Present(args);
            }
        }

        static void OnClusterDisplayUpdate()
        {
            // It may be possible that a subsystem update occurs after de-registration with the new update-loop being used next frame.
            if (TryGetInstance(out var clusterRenderer) &&
                ClusterCameraManager.Instance.ActiveCamera is Camera activeCamera &&
                clusterRenderer.m_ProjectionPolicy is ProjectionPolicy projectionPolicy)
            {
                projectionPolicy.UpdateCluster(clusterRenderer.m_Settings, activeCamera);
            }
        }
    }
}
