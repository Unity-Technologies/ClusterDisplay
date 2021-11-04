#if CLUSTER_DISPLAY_HDRP
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpClusterCameraController : ClusterCameraController
    {
        [HideInInspector]
        [SerializeField]
        bool m_PreviousAsymmetricProjectionSetting;
        [HideInInspector]
        [SerializeField]
        bool m_PreviousCustomFrameSettingsToggled;

        protected override void OnPollFrameSettings(Camera camera)
        {
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
        }
    }
}
#endif
