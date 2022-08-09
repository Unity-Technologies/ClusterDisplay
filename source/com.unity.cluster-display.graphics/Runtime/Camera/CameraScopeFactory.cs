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
        ClearHistory = 1 << 2,
        AsymmetricProjectionAndScreenCoordOverride = AsymmetricProjection | ScreenCoordOverride,
        All = ~0
    }

    static class CameraScopeFactory
    {
        // Used for C style (nested struct) inheritance, to avoid using classes.
        readonly struct BaseCameraScope
        {
            readonly Camera m_Camera;

            readonly Vector3 m_Position;
            readonly Quaternion m_Rotation;

            readonly RenderTexture m_Target;
            readonly bool m_UsePhysicalProperties;
            readonly int m_CullingMask;

            public BaseCameraScope(Camera camera)
            {
                m_Camera = camera;

                var transform = camera.transform;
                m_Position = transform.position;
                m_Rotation = transform.rotation;

                m_UsePhysicalProperties = camera.usePhysicalProperties;
                m_CullingMask = m_Camera.cullingMask;
                m_Target = m_Camera.targetTexture;
            }

            public void PreRender(RenderTexture target,
                Matrix4x4? projection,
                Vector3? position = null,
                Quaternion? rotation = null)
            {
                var transform = m_Camera.transform;

                var userModifiedProjectionMatrix = projection ?? m_Camera.projectionMatrix;
                var userModifiedPosition = position ?? transform.position;
                var userModifiedRotation = rotation ?? transform.rotation;

                m_Camera.targetTexture = target;

                transform.position = userModifiedPosition;
                transform.rotation = userModifiedRotation;
                m_Camera.projectionMatrix = userModifiedProjectionMatrix;

                m_Camera.cullingMatrix = userModifiedProjectionMatrix * m_Camera.worldToCameraMatrix;
                m_Camera.cullingMask = m_CullingMask & ~(1 << ClusterRenderer.VirtualObjectLayer);
            }

            public void Dispose()
            {
                // Restore the original transform
                var transform = m_Camera.transform;
                transform.position = m_Position;
                transform.rotation = m_Rotation;

                // Takes care of restoring the projection matrix
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
            readonly bool m_ForceClearHistory;

            public HdrpCameraScope(
                Camera camera,
                RenderFeature renderFeature)
            {
                m_BaseCameraScope = new BaseCameraScope(camera);
                m_Camera = camera;
                m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(camera.gameObject);
                m_HadCustomRenderSettings = m_AdditionalCameraData.customRenderingSettings;

                // Required, see ClusterCamera for assignment/restore/comment.
                Assert.IsTrue(m_AdditionalCameraData.hasPersistentHistory);

                var forceClearHistory = false;

                if (renderFeature != RenderFeature.None)
                {
                    m_AdditionalCameraData.customRenderingSettings = true;

                    if (renderFeature.HasFlag(RenderFeature.AsymmetricProjection))
                    {
                        m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int) FrameSettingsField.AsymmetricProjection] = true;
                        m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, true);
                    }

                    if (renderFeature.HasFlag(RenderFeature.ScreenCoordOverride))
                    {
                        m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int) FrameSettingsField.ScreenCoordOverride] = true;
                        m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, true);
                    }

                    if (renderFeature.HasFlag(RenderFeature.ClearHistory))
                    {
                        forceClearHistory = true;
                    }
                }

                m_ForceClearHistory = forceClearHistory;
            }

            public void Render(RenderTexture target,
                Matrix4x4? projection,
                Vector4? screenSizeOverride,
                Vector4? screenCoordScaleBias,
                Vector3? position,
                Quaternion? rotation)
            {
                m_BaseCameraScope.PreRender(target, projection, position, rotation);

                m_AdditionalCameraData.screenSizeOverride = screenSizeOverride ?? GraphicsUtil.k_IdentityScaleBias;
                m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias ?? GraphicsUtil.k_IdentityScaleBias;
                m_AdditionalCameraData.hasPersistentHistory = !m_ForceClearHistory;

                m_Camera.Render();
            }

            public void Dispose()
            {
                m_AdditionalCameraData.hasPersistentHistory = true;
                m_AdditionalCameraData.customRenderingSettings = m_HadCustomRenderSettings;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int) FrameSettingsField.AsymmetricProjection] = false;
                m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int) FrameSettingsField.ScreenCoordOverride] = false;
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

            public void Render(RenderTexture target,
                Matrix4x4? projection,
                Vector4? screenSizeOverride,
                Vector4? screenCoordScaleBias,
                Vector3? position,
                Quaternion? rotation)
            {
                m_BaseCameraScope.PreRender(target, projection, position, rotation);

                Debug.Assert(m_UseScreenCoordOverride == screenSizeOverride.HasValue);

                m_AdditionalCameraData.useScreenCoordOverride = m_UseScreenCoordOverride;
                m_AdditionalCameraData.screenSizeOverride = screenSizeOverride ?? GraphicsUtil.k_IdentityScaleBias;
                m_AdditionalCameraData.screenCoordScaleBias = screenCoordScaleBias ?? GraphicsUtil.k_IdentityScaleBias;

                m_Camera.Render();
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
            public void Render(RenderTexture target, Matrix4x4? projection, Vector4? screenSizeOverride, Vector4? screenCoordScaleBias, Vector3? position, Quaternion? rotation) { }
        }

        public static ICameraScope Create(
            Camera camera,
            RenderFeature renderFeature)
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
