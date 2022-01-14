using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    /// <summary>
    /// A utility to make sure the camera capture bridge works properly.
    /// (We have encountered bugs previously)
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class TestCameraCapture : MonoBehaviour
    {
        RenderTexture m_Target;
        CameraCapture m_Capture;

        void OnGUI()
        {
            if (m_Target != null)
            {
                GUI.DrawTexture(new Rect(0, 0, 256, 256), m_Target);
            }
        }

        void OnEnable()
        {
            GraphicsUtil.AllocateIfNeeded(ref m_Target, 256, 256);
            var @camera = GetComponent<Camera>();
            m_Capture = new CameraCapture(@camera, m_Target);
        }

        void OnDisable()
        {
            m_Capture.Dispose();
            GraphicsUtil.DeallocateIfNeeded(ref m_Target);
        }
    }
}
