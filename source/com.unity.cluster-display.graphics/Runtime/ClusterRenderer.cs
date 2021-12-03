using System;
using UnityEngine;
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

        public bool IsDebug
        {
            get => m_IsDebug;
            set => m_IsDebug = value;
        }

        public ClusterRendererSettings Settings => m_Settings;

        public void SetProjectionPolicy<T>() where T : ProjectionPolicy
        {
            if (m_ProjectionPolicy != null && m_ProjectionPolicy.gameObject == gameObject)
            {
                DestroyImmediate(m_ProjectionPolicy);
            }

            m_ProjectionPolicy = gameObject.AddComponent<T>();
            m_ProjectionPolicy.hideFlags = HideFlags.HideInInspector;
        }

        void OnDestroy()
        {
            if (m_ProjectionPolicy != null)
            {
                DestroyImmediate(m_ProjectionPolicy);
            }
        }

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
            m_ProjectionPolicy = GetComponent<ProjectionPolicy>();
            m_Presenter.Enable(gameObject);
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
            var activeCamera = ClusterCameraManager.Instance.ActiveCamera;
            if (activeCamera == null || m_ProjectionPolicy == null)
            {
                return;
            }

            m_ProjectionPolicy.UpdateCluster(m_Settings, activeCamera);
        }
    }
}
