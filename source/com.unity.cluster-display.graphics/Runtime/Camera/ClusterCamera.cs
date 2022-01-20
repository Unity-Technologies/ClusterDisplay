using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#elif CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#endif

using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    public static class ClusterCameraExtensions
    {
        public static bool TryGetClusterCamera (this Camera camera, out ClusterCamera clusterCamera)
        {
            if (camera == null)
            {
                clusterCamera = null;
                return false;
            }

            return (clusterCamera = camera.gameObject.GetComponent<ClusterCamera>()) != null;
        }
    }
    /// <summary>
    /// Attach this script to a Camera object if you wish for it to be rendered by
    /// the cluster.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClusterRenderer"/> will take control of rendering, so the
    /// <see cref="UnityEngine.Camera"/> component will be disabled (and you cannot enable it while
    /// this script is active.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways, DisallowMultipleComponent]
    public class ClusterCamera : MonoBehaviour
    {
        public delegate void UserPreCameraRenderDataOverride(
            int nodeId,
            ref Vector3 position,
            ref Quaternion rotation,
            ref Matrix4x4 projectionMatrix);

        public UserPreCameraRenderDataOverride userPreCameraRenderDataOverride;

        internal readonly struct QueuedCameraConfig
        {
            public readonly int m_NodeId;
            public readonly Matrix4x4 m_ProjectionMatrix;
            public readonly Vector4 m_ScreenSizeOverride;
            public readonly Vector4 m_ScreenCoordScaleBias;
            public readonly RenderTexture m_Target;
            public readonly RenderFeature m_RenderFeature;

            public QueuedCameraConfig(
                int nodeId,
                Matrix4x4 projectionMatrix,
                Vector4 screenSizeOverride,
                Vector4 screenCoordScaleBias,
                RenderTexture target,
                RenderFeature renderFeature)
            {
                m_NodeId = nodeId;
                m_ProjectionMatrix = projectionMatrix;
                m_ScreenSizeOverride = screenSizeOverride;
                m_ScreenCoordScaleBias = screenCoordScaleBias;
                m_Target = target;
                m_RenderFeature = renderFeature;
            }
        }

        private readonly struct QueuedCachedCameraConfig
        {
            #if CLUSTER_DISPLAY_HDRP
            public readonly bool m_UseCustomFrameSettings;
            public readonly bool m_HasPersistentHistory;
            #endif

            public readonly bool m_UsePhysicalCamera;
            public readonly int m_CachedCullingMask;
            public readonly Matrix4x4 m_CachedProjectionMatrix;
            public readonly Vector3 m_CachedPosition;
            public readonly Quaternion m_CachedRotation;
            public readonly RenderTexture m_CachedRenderTexture;

            public QueuedCachedCameraConfig(

                #if CLUSTER_DISPLAY_HDRP
                bool useCustomFrameSettings,
                bool hasPersistentHistory,
                #endif

                bool usePhysicalCamera,
                int cachedCullingMask,
                Matrix4x4 cachedProjectionMatrix,
                Vector3 cachedPosition,
                Quaternion cachedRotation,
                RenderTexture cachedRenderTexture)
            {
                #if CLUSTER_DISPLAY_HDRP
                m_UseCustomFrameSettings = useCustomFrameSettings;
                m_HasPersistentHistory = hasPersistentHistory;
                #endif
                m_UsePhysicalCamera = usePhysicalCamera;
                m_CachedCullingMask = cachedCullingMask;
                m_CachedProjectionMatrix = cachedProjectionMatrix;
                m_CachedPosition = cachedPosition;
                m_CachedRotation = cachedRotation;
                m_CachedRenderTexture = cachedRenderTexture;
            }
        }

        private readonly Queue<QueuedCameraConfig> m_QueuedCameraConfigs = new Queue<QueuedCameraConfig>();
        private readonly Queue<QueuedCachedCameraConfig> m_QueuedCachedCameraConfigs = new Queue<QueuedCachedCameraConfig>();

        Camera m_Camera;
        Camera cam
        {
            get
            {
                if (m_Camera == null)
                    m_Camera = GetComponent<Camera>();

                return m_Camera;
            }
        }

        #if CLUSTER_DISPLAY_HDRP
        HDAdditionalCameraData m_AdditionalCameraData;
        #elif CLUSTER_DISPLAY_URP
        UniversalAdditionalCameraData m_AdditionalCameraData;
        #endif

        private void Awake ()
        {
            cam.enabled = true;
        }

        void OnEnable()
        {

            #if CLUSTER_DISPLAY_HDRP
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(gameObject);
            #elif CLUSTER_DISPLAY_URP
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<UniversalAdditionalCameraData>(gameObject);
            #endif

            RegisterDelegates();
        }

        /*
        #if UNITY_EDITOR
        private void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (!cam.enabled)
                cam.enabled = true;
        }
        #endif
        */

        private void RegisterDelegates()
        {
            DeregisterDelegates();

            ClusterDebug.Log($"Registering {nameof(ClusterCamera)} delegates.");

            ClusterRenderer.onBeginFrameRender += OnBeginFrameRender;
            ClusterRenderer.onEndCameraRender += OnEndCameraRender;

            ClusterDisplayManager.onChangeActiveCamera += OnChangeActiveCamera;
            ClusterRenderer.onConfigureCamera += OnRenderClusterCamera;
        }

        private void DeregisterDelegates()
        {
            ClusterDebug.Log($"Deregistering {nameof(ClusterCamera)} delegates.");

            ClusterRenderer.onBeginFrameRender -= OnBeginFrameRender;
            ClusterRenderer.onEndCameraRender -= OnEndCameraRender;

            ClusterDisplayManager.onChangeActiveCamera -= OnChangeActiveCamera;
            ClusterRenderer.onConfigureCamera -= OnRenderClusterCamera;
        }

        private void OnDestroy() => DeregisterDelegates();

        private void OnChangeActiveCamera(Camera previousCamera, Camera nextCamera)
        {
            // If the previously active camera was this camera, reapply the user settings.
            if (previousCamera == cam)
            {
                m_QueuedCameraConfigs.Clear();
                // previousCamera.enabled = true;
            }

            // If the camera were cutting to is this camera, store the user settings.
            if (nextCamera == cam)
            {
            }
        }

        private void ConfigureCamera()
        {
            if (m_QueuedCameraConfigs.Count == 0)
                return;

            var cameraConfig = m_QueuedCameraConfigs.Dequeue();
            ClusterDebug.Log($"Configuring cluster camera: \"{cam.gameObject.name}\" on node: {cameraConfig.m_NodeId}.");

            var projectionMatrix = m_Camera.projectionMatrix;
            var position = m_Camera.transform.position;
            var rotation = m_Camera.transform.rotation;

            m_QueuedCachedCameraConfigs.Enqueue(new QueuedCachedCameraConfig(

                #if CLUSTER_DISPLAY_HDRP
                m_AdditionalCameraData.customRenderingSettings,
                m_AdditionalCameraData.hasPersistentHistory,
                #endif

                cam.usePhysicalProperties,
                cam.cullingMask,
                cam.projectionMatrix,
                position,
                rotation,
                cam.targetTexture));

            projectionMatrix = cameraConfig.m_ProjectionMatrix;
            userPreCameraRenderDataOverride?.Invoke(cameraConfig.m_NodeId, ref position, ref rotation, ref projectionMatrix);

            #if CLUSTER_DISPLAY_HDRP

            if (cameraConfig.m_RenderFeature != RenderFeature.None)
            {
                m_AdditionalCameraData.customRenderingSettings = true;

                if (cameraConfig.m_RenderFeature.HasFlag(RenderFeature.AsymmetricProjection))
                {
                    m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = true;
                    m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, true);
                }

                if (cameraConfig.m_RenderFeature.HasFlag(RenderFeature.ScreenCoordOverride))
                {
                    m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.ScreenCoordOverride] = true;
                    m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, true);
                }
            }
            m_AdditionalCameraData.hasPersistentHistory = true;

            #elif CLUSTER_DISPLAY_URP

            m_AdditionalCameraData.useScreenCoordOverride = cameraConfig.m_RenderFeature.HasFlag(RenderFeature.ScreenCoordOverride);

            #endif

            m_Camera.transform.position = position;
            m_Camera.transform.rotation = rotation;

            m_Camera.targetTexture = cameraConfig.m_Target;
            m_Camera.projectionMatrix = projectionMatrix;
            m_Camera.cullingMatrix = projectionMatrix * m_Camera.worldToCameraMatrix;
            m_Camera.cullingMask = cam.cullingMask & ~(1 << ClusterRenderer.VirtualObjectLayer);

            m_AdditionalCameraData.screenSizeOverride = cameraConfig.m_ScreenSizeOverride;
            m_AdditionalCameraData.screenCoordScaleBias = cameraConfig.m_ScreenCoordScaleBias;

        }

        private void OnBeginFrameRender(Camera camera)
        {
            if (this.cam != camera || !camera.gameObject.activeInHierarchy)
            {
                return;
            }

            ConfigureCamera();
        }

        private void OnEndCameraRender(Camera camera)
        {
            if (this.cam != camera)
            {
                return;
            }

            DeconfigureCamera();
        }

        private void OnRenderClusterCamera(
            Camera camera, 
            int nodeId, 
            Matrix4x4 projection, 
            Vector4 screenSizeOverride, 
            Vector4 screenCoordScaleBias, 
            RenderTexture target,
            RenderFeature renderFeature)
        {
            if (this.cam != camera || !camera.gameObject.activeInHierarchy)
            {
                return;
            }

            ClusterDebug.Log($"Queuing configuration for camera: \"{cam.gameObject.name}\" on node: {nodeId}.");
            // Queue camera data and config to be dequeued in OnBeginCameraRender()
            m_QueuedCameraConfigs.Enqueue(new QueuedCameraConfig(
                nodeId,
                projection,
                screenSizeOverride,
                screenCoordScaleBias,
                target,
                renderFeature));

            // If the camera is not enabled, then we need to explicitly call Render(), this is because
            // if your switching between camera A to camera B when B is enabled, OnBeginCameraRender()
            // will get executed automatically. We then disable the camera in OnBeginCameraRender
            // where we will explicitly call Render() from then on.
            if (!camera.enabled)
            {
                ClusterDebug.Log($"Active camera: \"{cam.gameObject.name}\" is disabled, so we will manually call render.");
                camera.Render();
            }
        }

        private void DeconfigureCamera ()
        {
            if (m_QueuedCachedCameraConfigs.Count == 0)
                return;

            ClusterDebug.Log($"Deconfiguring camera: \"{cam.gameObject.name}\".");

            var cachedCameraConfig = m_QueuedCachedCameraConfigs.Dequeue();
            ApplicationUtil.ResetCamera(cam);

            #if CLUSTER_DISPLAY_HDRP

            m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymmetricProjection] = false;
            m_AdditionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.ScreenCoordOverride] = false;
            m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymmetricProjection, false);
            m_AdditionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ScreenCoordOverride, false);
            m_AdditionalCameraData.customRenderingSettings = cachedCameraConfig.m_UseCustomFrameSettings;
            m_AdditionalCameraData.hasPersistentHistory = cachedCameraConfig.m_HasPersistentHistory;

            #elif CLUSTER_DISPLAY_URP

            m_AdditionalCameraData.useScreenCoordOverride = false;

            #endif

            cam.targetTexture = cachedCameraConfig.m_CachedRenderTexture;

            cam.transform.position = cachedCameraConfig.m_CachedPosition;
            cam.transform.rotation = cachedCameraConfig.m_CachedRotation;

            cam.projectionMatrix = cachedCameraConfig.m_CachedProjectionMatrix;
            cam.cullingMatrix = cam.projectionMatrix * m_Camera.worldToCameraMatrix;
            cam.cullingMask = cachedCameraConfig.m_CachedCullingMask;

            cam.usePhysicalProperties = cachedCameraConfig.m_UsePhysicalCamera;
        }
    }
}
