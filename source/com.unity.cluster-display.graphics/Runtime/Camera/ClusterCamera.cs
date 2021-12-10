using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
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

    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public static class ClusterCameraManager
    {
        static readonly List<Camera> m_ActiveCameras = new();

        // Programmer's note: ElementAtOrDefault() is one of the few non-allocating LINQ methods
        public static Camera ActiveCamera => m_ActiveCameras.ElementAtOrDefault(0);

        static ClusterCameraManager()
        {
            var cameras = Object.FindObjectsOfType<Camera>();
            foreach (var camera in cameras)
            {
                if (camera.GetComponent<ClusterCamera>() == null)
                    continue;
                Register(camera);
            }
        }

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
