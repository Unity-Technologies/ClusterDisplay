using System;
using UnityEngine;
using UnityEngine.Assertions;
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
        // Used for C style (nested struct) inheritance, to avoid using classes.
        readonly struct BaseCameraScope
        {
            readonly Camera m_Camera;
            readonly RenderTexture m_Target;
            readonly bool m_UsePhysicalProperties;
            readonly int m_CullingMask;

            public BaseCameraScope(Camera camera)
            {
                m_Camera = camera;
                m_UsePhysicalProperties = camera.usePhysicalProperties;
                m_CullingMask = m_Camera.cullingMask;
                m_Target = m_Camera.targetTexture;
            }

            public void PreRender(Matrix4x4 projection, RenderTexture target)
            {
                m_Camera.targetTexture = target;
                m_Camera.projectionMatrix = projection;
                m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
                m_Camera.cullingMask = m_CullingMask & ~(1 << ClusterRenderer.VirtualObjectLayer);
            }

            public void Dispose()
            {
                ApplicationUtil.ResetCamera(m_Camera);
                m_Camera.targetTexture = m_Target;
                m_Camera.cullingMask = m_CullingMask;
                m_Camera.usePhysicalProperties = m_UsePhysicalProperties;
            }
        }

#if CLUSTER_DISPLAY_HDRP
        readonly struct HdrpCameraScope : ICameraScope
        {
            readonly BaseCameraScope m_BaseCameraScope;
            readonly Camera m_Camera;
            readonly HDAdditionalCameraData m_AdditionalCameraData;
            readonly bool m_HadCustomRenderSettings;

            public HdrpCameraScope(Camera camera, RenderFeature renderFeature)
            {
                m_BaseCameraScope = new BaseCameraScope(camera);
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(camera.gameObject);
                m_HadCustomRenderSettings = m_AdditionalCameraData.customRenderingSettings;

                // Required, see ClusterCamera for assignment/restore/comment.
                Assert.IsTrue(m_AdditionalCameraData.hasPersistentHistory);

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
                m_BaseCameraScope.PreRender(projection, target);

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
                m_AdditionalCameraData.customRenderingSettings = m_HadCustomRenderSettings;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = false;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.ScreenCoordOverride] = false;
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, false);
                m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, false);

                m_BaseCameraScope.Dispose();
            }
        }
#elif CLUSTER_DISPLAY_URP
        readonly struct UrpCameraScope : ICameraScope
        {
            readonly BaseCameraScope m_BaseCameraScope;
            readonly Camera m_Camera;
            readonly UniversalAdditionalCameraData m_AdditionalCameraData;
            readonly bool m_UseScreenCoordOverride;

            public UrpCameraScope(Camera camera, RenderFeature renderFeature)
            {
                m_BaseCameraScope = new BaseCameraScope(camera);
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                m_UseScreenCoordOverride = renderFeature.HasFlag(RenderFeature.ScreenCoordOverride);
            }

            public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
            {
                m_BaseCameraScope.PreRender(projection, target);

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
                m_AdditionalCameraData.useScreenCoordOverride = false;
                m_BaseCameraScope.Dispose();
            }
        }
#endif
        readonly struct NullCameraScope : ICameraScope
        {
            public void Dispose() { }

            public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target) { }

            public void Render(Matrix4x4 projection, RenderTexture target) { }
        }

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
