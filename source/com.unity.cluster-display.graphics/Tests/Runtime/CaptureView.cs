using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    // A utility that helps debugging tests themselves.
    /*public class CaptureView : MonoBehaviour
    {
        
        [SerializeField]
        Vector2Int m_Size;

        [SerializeField]
        bool m_ShowGUI = true;

        RenderTexture m_MainCameraCapture;
        RenderTexture m_ClusterCapture;

        void OnValidate()
        {
            m_Size.x = Mathf.Max(32, m_Size.x);
            m_Size.y = Mathf.Max(32, m_Size.y);
        }

        void OnGUI()
        {
            if (!m_ShowGUI)
            {
                return;
            }

            if (m_MainCameraCapture != null)
            {
                var rect = new Rect(0, 0, m_Size.x, m_Size.y);
                GUI.DrawTexture(rect, m_MainCameraCapture);
            }

            if (m_ClusterCapture != null)
            {
                var rect = new Rect(m_Size.x, 0, m_Size.x, m_Size.y);
                GUI.DrawTexture(rect, m_ClusterCapture);
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            GraphicsUtil.DeallocateIfNeeded(ref m_MainCameraCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCapture);
        }

        [ContextMenu("Capture Main Camera")]
        void CaptureMainCamera()
        {
            StartCoroutine(CaptureMainCameraCoroutine());
        }

        [ContextMenu("Capture Cluster Renderer")]
        void CaptureClusterRenderer()
        {
            StartCoroutine(CaptureClusterRendererCoroutine());
        }

        IEnumerator CaptureMainCameraCoroutine()
        {
            if (Camera.main == null)
            {
                Debug.LogError("Could not access main camera.");
                yield break;
            }

            GraphicsUtil.AllocateIfNeeded(ref m_MainCameraCapture, m_Size.x, m_Size.y);
            using (new CameraCapture(Camera.main, m_MainCameraCapture))
            {
                yield return new WaitForEndOfFrame();
            }
        }

        IEnumerator CaptureClusterRendererCoroutine()
        {
            if (ClusterRenderer.TryGetInstance(out var clusterRenderer) &&
                clusterRenderer.PresentCamera is Camera presentCamera)
            {
                GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Size.x, m_Size.y);
                using (new CameraCapture(presentCamera, m_ClusterCapture))
                {
                    yield return new WaitForEndOfFrame();
                }
            }
            else
            {
                Debug.LogError($"Could not access {nameof(ClusterRenderer)} Presenter camera.");
            }
        }
    }*/
}
