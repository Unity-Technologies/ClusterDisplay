using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Attach this script to a Camera object if you wish for it to be rendered by
    /// the cluster.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClusterRenderer"/> will take control of rendering, so the
    /// <see cref="Camera"/> component will be disabled (and you cannot enable it while
    /// this script is active.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways, DisallowMultipleComponent]
    public class ClusterCamera : MonoBehaviour
    {
        Camera m_Camera;

        void Update()
        {
            m_Camera.enabled = false;
        }

        void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
            ClusterCameraManager.Register(m_Camera);
        }

        void OnDisable()
        {
            ClusterCameraManager.Unregister(m_Camera);
        }
    }

    public static class ClusterCameraManager
    {
        readonly static List<Camera> m_ActiveCameras = new();

        public static Camera ActiveCamera => m_ActiveCameras.ElementAtOrDefault(0);

        public static void Register(Camera camera)
        {
            if (!m_ActiveCameras.Contains(camera))
            {
                m_ActiveCameras.Add(camera);
            }
        }

        public static void Unregister(Camera camera)
        {
            m_ActiveCameras.Remove(camera);
        }
    }
}
