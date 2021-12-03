using UnityEngine;

#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#elif CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    static class CameraScopeFactory
    {
#if CLUSTER_DISPLAY_HDRP
        readonly struct HdrpCameraScope : ICameraScope
        {
            readonly Camera m_Camera;
            readonly HDAdditionalCameraData m_AdditionalCameraData;
            readonly bool m_HadCustomRenderSettings;

            public HdrpCameraScope(Camera camera)
            {
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(camera.gameObject);
                m_HadCustomRenderSettings = m_AdditionalCameraData.customRenderingSettings;
            
                m_AdditionalCameraData.customRenderingSettings = true;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = true;
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, true);
            }

            public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
            {
                m_Camera.targetTexture = target;
                m_Camera.projectionMatrix = projection;
                m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;

                m_AdditionalCameraData.screenSizeOverride = screenSizeOverride;
                m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias;

                m_Camera.Render();
            }

            public void Dispose()
            {
                m_AdditionalCameraData.customRenderingSettings = m_HadCustomRenderSettings;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = false;
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, false);

                m_Camera.ResetAspect();
                m_Camera.ResetProjectionMatrix();
                m_Camera.ResetCullingMatrix();
            }
        }
#elif CLUSTER_DISPLAY_URP
    readonly struct UrpCameraScope : ICameraScope
    {
        readonly Camera m_Camera;
        readonly UniversalAdditionalCameraData m_AdditionalCameraData;

        public UrpCameraScope(Camera camera)
        {
            m_Camera = camera;
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<UniversalAdditionalCameraData>(camera.gameObject);
        }

        public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
        {
            m_Camera.targetTexture = target;
            m_Camera.projectionMatrix = projection;
            m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
            
            m_AdditionalCameraData.screenSizeOverride = screenSizeOverride;
            m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias;

            m_Camera.Render();
        }
            
        public void Dispose()
        {
            m_Camera.ResetAspect();
            m_Camera.ResetProjectionMatrix();
            m_Camera.ResetCullingMatrix();
        }
    }
#else
        readonly struct NullCameraScope : ICameraScope
        {
            public void Dispose()
            {
            }

            public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
            {
            }
        }
#endif

        public static ICameraScope Create(Camera camera)
        {
#if CLUSTER_DISPLAY_HDRP
            return new HdrpCameraScope(camera);
#elif CLUSTER_DISPLAY_URP
            return new UrpCameraScope(camera);
#else // TODO Add support for legacy render pipeline.
            return new NullCameraScope();
#endif
        }
    }
}
