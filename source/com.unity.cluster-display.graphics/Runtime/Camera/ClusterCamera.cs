using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
            ClusterCameraManager.Instance.Register(m_Camera);
        }

        void OnDisable()
        {
            ClusterCameraManager.Instance.Unregister(m_Camera);
        }
    }

    public class ClusterCameraManager
    {
        readonly List<Camera> m_ActiveCameras = new();

        public static ClusterCameraManager Instance { get; } = new();

        // Programmer's note: ElementAtOrDefault() is one of the few non-allocating LINQ methods
        public Camera ActiveCamera => m_ActiveCameras.ElementAtOrDefault(0);

        public void Register(Camera camera)
        {
            if (!m_ActiveCameras.Contains(camera))
            {
                m_ActiveCameras.Add(camera);
            }
        }

        public void Unregister(Camera camera)
        {
            m_ActiveCameras.Remove(camera);
        }
    }
}
