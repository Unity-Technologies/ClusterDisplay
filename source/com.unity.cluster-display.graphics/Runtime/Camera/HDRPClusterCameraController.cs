#if CLUSTER_DISPLAY_HDRP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class HDRPClusterCameraController : ClusterCameraController
    {
        [HideInInspector][SerializeField] private bool m_PreviousAsymmetricProjectionSetting;
        [HideInInspector][SerializeField] private bool m_PreviousCustomFrameSettingsToggled;
        [HideInInspector][SerializeField] private HDAdditionalCameraData.AntialiasingMode m_PreviousAntiAliasingMode;

        protected override void OnPollFrameSettings(Camera camera)
        {
            if (camera.TryGetComponent<HDAdditionalCameraData>(out var additionalCameraData))
            {
<<<<<<< HEAD
                if (previousContextCamera != null)
=======
                if (TryGetPreviousCameraContext(out var previousContextCamera))
>>>>>>> 2d66b570e0aed08c93a752d6ed14377986100698
                {
                    additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = m_PreviousAsymmetricProjectionSetting;
                    additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, m_PreviousAsymmetricProjectionSetting);
                    additionalCameraData.customRenderingSettings = m_PreviousCustomFrameSettingsToggled;
                }

<<<<<<< HEAD
                if (contextCamera != null && contextCamera.TryGetComponent(out additionalCameraData))
=======
                if (TryGetContextCamera(out var contextCamera) && contextCamera.TryGetComponent(out additionalCameraData))
>>>>>>> 2d66b570e0aed08c93a752d6ed14377986100698
                {
                    m_PreviousAsymmetricProjectionSetting = additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection];
                    m_PreviousCustomFrameSettingsToggled = additionalCameraData.customRenderingSettings;
                    m_PreviousAntiAliasingMode = additionalCameraData.antialiasing;

                    additionalCameraData.customRenderingSettings = true;
                    additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = true;
                    additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, true);
                    additionalCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
                }
            }

            else Debug.LogErrorFormat($"{nameof(HDCamera)} does not have {nameof(HDAdditionalCameraData)} component attached, refusing to change context.");
        }
    }
}
#endif
