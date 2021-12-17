using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Defines a physical planar projection surface.
    /// </summary>
    [Serializable]
    public struct ProjectionSurface : IEquatable<ProjectionSurface>
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
        /// Base (untransformed) vertices of the surface (i.e. unit size).
        /// </summary>
        [SerializeField]
        Vector3[] m_Vertices;

        /// <summary>
        /// Indices used to draw the surface as a polygon.
        /// </summary>
        [SerializeField]
        int[] m_DrawOrder;

        /// <summary>
        /// Indices of 4 vertices that form a right-angled plane, in anti-clockwise
        /// order starting at the bottom-left.
        /// </summary>
        /// <remarks>
        /// Given in the following order: [bottom-left, bottom-right, top-right, top-left].
        /// </remarks>
        [SerializeField]
        int[] m_PlaneWinding;

        /// <summary>
        /// Whether this item is expanded in the Editor.
        /// </summary>
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
                m_PlaneWinding = new[] {0, 1, 3, 2},
                m_DrawOrder = new[] {0, 1, 0, 2, 1, 3, 2, 3},
                m_Expanded = true
            };
        }

        /// <summary>
        /// A data structure for holding 4 points.
        /// </summary>
        public readonly struct FrustumPlane
        {
            public readonly Vector3 BottomLeft;
            public readonly Vector3 BottomRight;
            public readonly Vector3 TopLeft;

            // Note: over-constrained.
            public readonly Vector3 TopRight;

            public FrustumPlane(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight)
            {
                BottomLeft = bottomLeft;
                BottomRight = bottomRight;
                TopLeft = topLeft;
                TopRight = topRight;
            }

            public FrustumPlane ApplyTransform(Matrix4x4 transform)
            {
                return new FrustumPlane(
                    bottomLeft: transform.MultiplyPoint(BottomLeft),
                    bottomRight: transform.MultiplyPoint(BottomRight),
                    topLeft: transform.MultiplyPoint(TopLeft),
                    topRight: transform.MultiplyPoint(TopRight));
            }
        }

        public Vector3 Scale => new(PhysicalSize.x, PhysicalSize.y, 1);

        /// <summary>
        /// Indices used to draw the surface as a polygon.
        /// </summary>
        internal int[] DrawOrder => m_DrawOrder;

        /// <summary>
        /// Get vertices in a world coordinate system.
        /// </summary>
        /// <param name="rootTransform">The anchor (root) transform.</param>
        /// <returns></returns>
        internal Vector3[] GetVertices(Matrix4x4 rootTransform)
        {
            if (m_Vertices == null)
            {
                return Array.Empty<Vector3>();
            }
            
            var surfaceTransform = rootTransform * Matrix4x4.TRS(LocalPosition, LocalRotation, Scale);

            var vertsWorld = new Vector3[m_Vertices.Length];
            for (var i = 0; i < m_Vertices.Length; i++)
            {
                vertsWorld[i] = surfaceTransform.MultiplyPoint(m_Vertices[i]);
            }

            return vertsWorld;
        }

        /// <summary>
        /// Get the vertices of a right-angled plane that covers the projection surface.
        /// </summary>
        /// <param name="rootTransform">The anchor (root) transform.</param>
        /// <returns></returns>
        internal FrustumPlane GetFrustumPlane(Matrix4x4 rootTransform)
        {
            if (m_Vertices == null || m_Vertices.Length < 4)
            {
                // This is not a sufficient check, but it's an efficient one.
                return new FrustumPlane();
            }
            
            var surfaceTransform = rootTransform * Matrix4x4.TRS(LocalPosition, LocalRotation, Scale);

            return new FrustumPlane(
                bottomLeft: m_Vertices[m_PlaneWinding[0]],
                bottomRight: m_Vertices[m_PlaneWinding[1]],
                topLeft: m_Vertices[m_PlaneWinding[3]],
                topRight: m_Vertices[m_PlaneWinding[2]]
            ).ApplyTransform(surfaceTransform);
        }

        public bool Equals(ProjectionSurface other)
        {
            return Name == other.Name &&
                ScreenResolution.Equals(other.ScreenResolution) &&
                PhysicalSize.Equals(other.PhysicalSize) &&
                LocalPosition.Equals(other.LocalPosition) &&
                LocalRotation.Equals(other.LocalRotation);
        }

        public static bool operator ==(ProjectionSurface left, ProjectionSurface right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProjectionSurface left, ProjectionSurface right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectionSurface other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ ScreenResolution.GetHashCode();
                hashCode = (hashCode * 397) ^ PhysicalSize.GetHashCode();
                hashCode = (hashCode * 397) ^ LocalPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ LocalRotation.GetHashCode();
                return hashCode;
            }
        }
    }
}
