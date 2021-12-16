using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics
{
    [Serializable]
    public struct ProjectionSurface
    {
        [SerializeField]
        public string Name;

        /// <summary>
        /// The output resolution of the screen.
        /// </summary>
        [SerializeField]
        public Vector2Int ScreenResolution;

        /// <summary>
        /// Physical size of the screen in world units.
        /// </summary>
        [SerializeField]
        public Vector2 PhysicalSize;

        /// <summary>
        /// Position of the center of the screen relative to the anchor.
        /// </summary>
        [SerializeField]
        public Vector3 LocalPosition;

        /// <summary>
        /// Rotation of the screen relative to the anchor.
        /// </summary>
        [SerializeField]
        public Quaternion LocalRotation;

        /// <summary>
        /// Base (untransformed) vertices of the surface.
        /// </summary>
        [SerializeField]
        Vector3[] m_Vertices;

        [SerializeField]
        bool m_Expanded;

        public static ProjectionSurface CreateDefaultPlanar(string name)
        {
            return new ProjectionSurface
            {
                Name = name,
                ScreenResolution = new Vector2Int(1920, 1080),
                PhysicalSize = new Vector2(4.8f, 2.7f),
                LocalPosition = Vector3.forward * 3f,
                LocalRotation = Quaternion.Euler(0, 180, 0),
                m_Vertices = new Vector3[]
                {
                    new(0.5f, -0.5f, 0),
                    new(-0.5f, -0.5f, 0),
                    new(0.5f, 0.5f, 0),
                    new(-0.5f, 0.5f, 0)
                },
                m_Expanded = true
            };
        }

        public Vector3 Scale => new(PhysicalSize.x, PhysicalSize.y, 1);

        internal Vector3[] GetVertices(Matrix4x4 rootTransform)
        {
            var surfaceTransform = rootTransform * Matrix4x4.TRS(LocalPosition, LocalRotation, Scale);

            var cornersWorld = new Vector3[m_Vertices.Length];
            for (var i = 0; i < m_Vertices.Length; i++)
            {
                cornersWorld[i] = surfaceTransform.MultiplyPoint(m_Vertices[i]);
            }

            return cornersWorld;
        }
    }
}
