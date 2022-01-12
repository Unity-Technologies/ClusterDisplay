using System;
using UnityEngine;

#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#elif CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    [Flags]
    enum RenderFeature
    {
        None = 0,
        AsymmetricProjection = 1 << 0,
        ScreenCoordOverride = 1 << 1,
        AsymmetricProjectionAndScreenCoordOverride = AsymmetricProjection | ScreenCoordOverride,
        All = ~0
    }

    static class CameraScopeFactory
    {
#if CLUSTER_DISPLAY_HDRP
        readonly struct HdrpCameraScope : ICameraScope
        {
            readonly Camera m_Camera;
            readonly HDAdditionalCameraData m_AdditionalCameraData;
            readonly bool m_HadCustomRenderSettings;

            public HdrpCameraScope(Camera camera, RenderFeature renderFeature)
            {
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(camera.gameObject);
                m_HadCustomRenderSettings = m_AdditionalCameraData.customRenderingSettings;
            
                // TODO Should we cache frame settings to restore them on Dispose?
                if (renderFeature != RenderFeature.None)
                {
                    m_AdditionalCameraData.customRenderingSettings = true;

                    if (renderFeature.HasFlag(RenderFeature.AsymmetricProjection))
                    {
                        m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = true;
                        m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, true);
                    }

                    if (renderFeature.HasFlag(RenderFeature.ScreenCoordOverride))
                    {
                        m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.ScreenCoordOverride] = true;
                        m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, true);
                    }
                }
                
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
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.ScreenCoordOverride] = false;
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, false);
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, false);

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
        readonly bool m_UseScreenCoordOverride;
        readonly int m_CullingMask;

        public UrpCameraScope(Camera camera, RenderFeature renderFeature)
        {
            m_Camera = camera;
            m_CullingMask = m_Camera.cullingMask;
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            m_UseScreenCoordOverride = renderFeature.HasFlag(RenderFeature.ScreenCoordOverride);
        }

        public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
        {
            m_Camera.targetTexture = target;
            m_Camera.projectionMatrix = projection;
            m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;

            m_Camera.cullingMask = m_CullingMask & ~(1 << ClusterRenderer.VirtualObjectLayer);

            m_AdditionalCameraData.useScreenCoordOverride = m_UseScreenCoordOverride;
            m_AdditionalCameraData.screenSizeOverride = screenSizeOverride;
            m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias;

            m_Camera.Render();
        }

        public void Render(Matrix4x4 projection, RenderTexture target)
        {
            Render(projection, GraphicsUtil.k_IdentityScaleBias, GraphicsUtil.k_IdentityScaleBias, target);
        }

        public void Dispose()
        {
            // TODO Should we save & restore instead of setting false?
            m_AdditionalCameraData.useScreenCoordOverride = false;

            m_Camera.ResetAspect();
            m_Camera.ResetProjectionMatrix();
            m_Camera.ResetCullingMatrix();
            m_Camera.cullingMask = m_CullingMask;
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

            public void Render(Matrix4x4 projection, RenderTexture target)
            {
            }
        }
#endif

        public static ICameraScope Create(Camera camera, RenderFeature renderFeature)
        {
#if CLUSTER_DISPLAY_HDRP
            return new HdrpCameraScope(camera, renderFeature);
#elif CLUSTER_DISPLAY_URP
            return new UrpCameraScope(camera, renderFeature);
#else // TODO Add support for legacy render pipeline.
            return new NullCameraScope();
#endif
        }
    }
}
