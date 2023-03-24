using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class DebugCanvasWorldSpaceCameraOverlay : MonoBehaviour
    {
        Canvas m_RootCanvas;
        RectTransform m_RootCanvasTransform;
        public Vector2 resolution = new(3840, 2160);
        public float nearPlanOffset = 0.001f;

        void OnEnable()
        {
            m_RootCanvas = gameObject.GetComponent<Canvas>().rootCanvas;
            m_RootCanvasTransform = (RectTransform)m_RootCanvas.transform;
            m_RootCanvas.renderMode = RenderMode.WorldSpace;
            m_RootCanvasTransform.sizeDelta = resolution;

            RenderPipelineManager.beginCameraRendering += RenderPipelineManagerOnBeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderPipelineManagerOnBeginCameraRendering;

            m_RootCanvas = null;
        }

        void RenderPipelineManagerOnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer) ||
                !clusterRenderer.isActiveAndEnabled ||
                !ReferenceEquals(cam, ClusterCameraManager.Instance.ActiveCamera))
            {
                return;
            }

            if (!ReferenceEquals(cam, ClusterCameraManager.Instance.ActiveCamera))
            {
                return;
            }

            float nearClipPlaneDistance = cam.nearClipPlane;
            float overscanPixels = clusterRenderer.Settings.OverScanInPixels;
            Vector2 viewportOverscan = new(overscanPixels / cam.pixelWidth, overscanPixels / cam.pixelHeight);
            var center = cam.ViewportToWorldPoint(new(0.5f, 0.5f, nearClipPlaneDistance));
            var right = cam.ViewportToWorldPoint(new(1 - viewportOverscan.x, 0.5f, nearClipPlaneDistance));
            var top = cam.ViewportToWorldPoint(new(0.5f, 1 - viewportOverscan.y, nearClipPlaneDistance));

            var canvasUpVector = (top - center).normalized;
            var fromCanvasCenterToCamera = Vector3.Cross((right - center).normalized, canvasUpVector);
            m_RootCanvasTransform.rotation = Quaternion.LookRotation(fromCanvasCenterToCamera, canvasUpVector);

            m_RootCanvasTransform.localPosition = cam.ViewportToWorldPoint(new(0.5f, 0.5f, nearClipPlaneDistance + nearPlanOffset));

            var size = new Vector2((right - center).magnitude * 2, (top - center).magnitude * 2);
            var sizeDelta = m_RootCanvasTransform.sizeDelta;
            m_RootCanvasTransform.localScale = new (size.x / sizeDelta.x, size.y / sizeDelta.y, 1);
        }
    }
}
