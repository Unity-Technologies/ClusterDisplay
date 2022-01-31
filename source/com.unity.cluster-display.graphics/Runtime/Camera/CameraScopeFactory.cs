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
            readonly ClusterRenderer.UserPreCameraRenderDataOverride m_UserPreCameraRenderDataOverride;

            readonly Vector3 m_Position;
            readonly Quaternion m_Rotation;

            readonly RenderTexture m_Target;
            readonly bool m_UsePhysicalProperties;
            readonly int m_CullingMask;

            public BaseCameraScope(
                Camera camera, 
                ClusterRenderer.UserPreCameraRenderDataOverride userPreCameraRenderDataOverride)
            {
                m_Camera = camera;
                m_UserPreCameraRenderDataOverride = userPreCameraRenderDataOverride;

                m_Position = camera.transform.position;
                m_Rotation = camera.transform.rotation;

                m_UsePhysicalProperties = camera.usePhysicalProperties;
                m_CullingMask = m_Camera.cullingMask;
                m_Target = m_Camera.targetTexture;
            }

            public void PreRender(int nodeID, Matrix4x4 projection, RenderTexture target)
            {
                var userModifiedProjectionMatrix = projection;
                var userModifiedPosition = m_Camera.transform.position;
                var userModifiedRotation = m_Camera.transform.rotation;

                m_UserPreCameraRenderDataOverride?.Invoke(nodeID, ref userModifiedPosition, ref userModifiedRotation, ref userModifiedProjectionMatrix);

                m_Camera.targetTexture = target;

                m_Camera.transform.position = userModifiedPosition;
                m_Camera.transform.rotation = userModifiedRotation;
                m_Camera.projectionMatrix = userModifiedProjectionMatrix;

                m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
                m_Camera.cullingMask = m_CullingMask & ~(1 << ClusterRenderer.VirtualObjectLayer);
            }

            public void Dispose()
            {
                m_Camera.transform.position = m_Position;
                m_Camera.transform.rotation = m_Rotation;

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

            public HdrpCameraScope(
                Camera camera, 
                RenderFeature renderFeature,
                ClusterRenderer.UserPreCameraRenderDataOverride userPreCameraRenderDataOverride)
            {
                m_BaseCameraScope = new BaseCameraScope(camera, userPreCameraRenderDataOverride);
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

            public void Render(int nodeID, Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
            {
                m_BaseCameraScope.PreRender(nodeID, projection, target);

                m_AdditionalCameraData.screenSizeOverride = screenSizeOverride;
                m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias;

                m_Camera.Render();
            }

            public void Render(int nodeID, Matrix4x4 projection, RenderTexture target)
            {
                Render(nodeID, projection, GraphicsUtil.k_IdentityScaleBias, GraphicsUtil.k_IdentityScaleBias, target);
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

            public UrpCameraScope(
                Camera camera, 
                RenderFeature renderFeature, 
                ClusterRenderer.UserPreCameraRenderDataOverride userPreCameraRenderDataOverride)
            {
                m_BaseCameraScope = new BaseCameraScope(camera, userPreCameraRenderDataOverride);
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                m_UseScreenCoordOverride = renderFeature.HasFlag(RenderFeature.ScreenCoordOverride);
            }

            public void Render(int nodeID, Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target)
            {
                m_BaseCameraScope.PreRender(nodeID, projection, target);

                m_AdditionalCameraData.useScreenCoordOverride = m_UseScreenCoordOverride;
                m_AdditionalCameraData.screenSizeOverride = screenSizeOverride;
                m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias;

                m_Camera.Render();
            }

            public void Render(int nodeID, Matrix4x4 projection, RenderTexture target)
            {
                Render(nodeID, projection, GraphicsUtil.k_IdentityScaleBias, GraphicsUtil.k_IdentityScaleBias, target);
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

            public void Render(int nodeID, Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target) { }

            public void Render(int nodeID, Matrix4x4 projection, RenderTexture target) { }
        }

        public static ICameraScope Create(
            Camera camera, 
            RenderFeature renderFeature,
            ClusterRenderer.UserPreCameraRenderDataOverride userPreCameraRenderDataOverride)
        {
#if CLUSTER_DISPLAY_HDRP
            return new HdrpCameraScope(camera, renderFeature, userPreCameraRenderDataOverride);
#elif CLUSTER_DISPLAY_URP
            return new UrpCameraScope(camera, renderFeature, userPreCameraRenderDataOverride);
#else // TODO Add support for legacy render pipeline.
            return new NullCameraScope();
#endif
        }
    }
}
