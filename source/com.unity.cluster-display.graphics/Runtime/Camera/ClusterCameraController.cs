using System;
using UnityEngine;
using UnityEngine.Rendering;
#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    interface ICameraEventReceiver
    {
        void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
    }

    [Serializable]
    class ClusterCameraController : IClusterRendererEventReceiver
    {
        // Matrix4x4 does not serialize so we need to serialize to Vector4s.
        [SerializeField]
        Vector4 m_SerializedProjectionMatrixC1 = Vector4.zero;
        [SerializeField]
        Vector4 m_SerializedProjectionMatrixC2 = Vector4.zero;
        [SerializeField]
        Vector4 m_SerializedProjectionMatrixC3 = Vector4.zero;
        [SerializeField]
        Vector4 m_SerializedProjectionMatrixC4 = Vector4.zero;

        [HideInInspector]
        [SerializeField]
        bool m_PreviousAsymmetricProjectionSetting;
        [HideInInspector]
        [SerializeField]
        bool m_PreviousCustomFrameSettingsToggled;
        
        /// <summary>
        /// Current rendering camera.
        /// </summary>
        public bool TryGetContextCamera(out Camera contextCamera)
        {
            contextCamera = null;
            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry))
            {
                return false;
            }

            if (!cameraContextRegistry.TryGetFocusedCameraContextTarget(out var focusedCameraContextTarget))
            {
                return false;
            }

            if (!focusedCameraContextTarget.TryGetCamera(out var camera))
            {
                cameraContextRegistry.UnRegister(focusedCameraContextTarget, true);
                return false;
            }

            return (contextCamera = camera) != null;
        }

        public bool TryGetPreviousCameraContext(out Camera previousCameraContext)
        {
            previousCameraContext = null;
            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry))
            {
                return false;
            }

            if (!cameraContextRegistry.TryGetPreviousFocusedCameraContextTarget(out var previousFocusedCameraContextTarget))
            {
                return false;
            }

            if (!previousFocusedCameraContextTarget.TryGetCamera(out var camera))
            {
                cameraContextRegistry.UnRegister(previousFocusedCameraContextTarget, true);
                return false;
            }

            return (previousCameraContext = camera) != null;
        }

        event Action<Camera, Camera> onCameraChange;

        Presenter m_Presenter;

        public Presenter Presenter
        {
            get => m_Presenter;
            set
            {
                if (m_Presenter != null)
                {
                    UnRegisterCameraEventReceiver(m_Presenter);
                    m_Presenter.Dispose();
                }

                m_Presenter = value;
                if (m_Presenter != null)
                {
                    RegisterCameraEventReceiver(m_Presenter);
                }
            }
        }

        public bool CameraIsInContext(Camera camera)
        {
            return TryGetContextCamera(out var contextCamera) && contextCamera == camera;
        }

        public void RegisterCameraEventReceiver(ICameraEventReceiver cameraEventReceiver)
        {
            onCameraChange += cameraEventReceiver.OnCameraContextChange;
        }

        public void UnRegisterCameraEventReceiver(ICameraEventReceiver cameraEventReceiver)
        {
            onCameraChange -= cameraEventReceiver.OnCameraContextChange;
        }

        void OnPollFrameSettings(Camera camera)
        {
#if CLUSTER_DISPLAY_HDRP
            
            if (camera.TryGetComponent<HDAdditionalCameraData>(out var additionalCameraData))
            {
                if (TryGetPreviousCameraContext(out _))
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
                }
            }
            else
            {
                Debug.LogErrorFormat($"{nameof(HDCamera)} does not have {nameof(HDAdditionalCameraData)} component attached, refusing to change context.");
            }
#endif
        }

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }

        public void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!CameraContextRegistry.CanChangeContextTo(camera))
            {
                return;
            }

            // If we are beginning to render with our context camera, do nothing.
            if (TryGetContextCamera(out var contextCamera) && camera == contextCamera)
            {
                m_Presenter.PollCamera(contextCamera);
                return;
            }

            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry) ||
                !cameraContextRegistry.TryGetCameraContextTarget(camera, out var cameraContextTarget))
            {
                m_Presenter.PollCamera(contextCamera);
                return;
            }

            cameraContextRegistry.SetFocusedCameraContextTarget(cameraContextTarget);
            OnPollFrameSettings(camera);

            TryGetPreviousCameraContext(out var previousCameraContext);
            if (onCameraChange != null)
            {
                onCameraChange(previousCameraContext, contextCamera);
            }

            m_Presenter.PollCamera(contextCamera);
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }
    }
}
