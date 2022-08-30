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
        /// Base (untransformed) vertices of a surface (i.e. unit size).
        /// </summary>
        static readonly Vector3[] k_UnitPlaneVerts = {
            new(0.5f, -0.5f, 0),
            new(-0.5f, -0.5f, 0),
            new(0.5f, 0.5f, 0),
            new(-0.5f, 0.5f, 0)
        };
        
        /// <summary>
        /// Indices of 4 vertices that form a right-angled plane, in anti-clockwise
        /// order starting at the bottom-left.
        /// </summary>
        /// <remarks>
        /// Given in the following order: [bottom-left, bottom-right, top-right, top-left].
        /// </remarks>
        static readonly int[] k_UnitPlaneWinding = {0, 1, 3, 2};
        
        /// <summary>
        /// Indices used to draw the surface as a polygon.
        /// </summary>
        static readonly int[] k_UnitPlaneDrawIndices = {0, 1, 3, 2, 0};

        /// <summary>
        /// Creates a planar projection surface defaults for size, orientation, and position.
        /// </summary>
        /// <param name="name">Name of the surface.</param>
        /// <returns></returns>
        public static ProjectionSurface Create(string name)
        {
            return new ProjectionSurface
            {
                Name = name,
                ScreenResolution = new Vector2Int(1920, 1080),
                PhysicalSize = new Vector2(4.8f, 2.7f),
                LocalPosition = Vector3.forward * 3f,
                LocalRotation = Quaternion.Euler(0, 180, 0)
            };
        }

        /// <summary>
        /// A data structure for holding 4 points.
        /// </summary>
        internal readonly struct FrustumPlane
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
        internal int[] Indices => k_UnitPlaneDrawIndices;

        /// <summary>
        /// Get vertices in a world coordinate system.
        /// </summary>
        /// <param name="rootTransform">The anchor (root) transform.</param>
        /// <returns></returns>
        internal Vector3[] GetVertices(Matrix4x4 rootTransform)
        {
            Debug.Assert(k_UnitPlaneVerts != null);
            
            var surfaceTransform = rootTransform * Matrix4x4.TRS(LocalPosition, LocalRotation, Scale);

            var vertsWorld = new Vector3[k_UnitPlaneVerts.Length];
            for (var i = 0; i < k_UnitPlaneVerts.Length; i++)
            {
                vertsWorld[i] = surfaceTransform.MultiplyPoint(k_UnitPlaneVerts[i]);
            }

            return vertsWorld;
        }

        internal Vector3[] GetPolyLine(Matrix4x4 rootTransForm)
        {
            var verts = GetVertices(rootTransForm);
            var polyline = new Vector3[Indices.Length];
            for (var i = 0; i < Indices.Length; i++)
            {
                polyline[i] = verts[Indices[i]];
            }

            return polyline;
        }

        /// <summary>
        /// Get the vertices of a right-angled plane that covers the projection surface.
        /// </summary>
        /// <param name="rootTransform">The anchor (root) transform.</param>
        /// <returns></returns>
        internal FrustumPlane GetFrustumPlane(Matrix4x4 rootTransform)
        {
            Debug.Assert(k_UnitPlaneVerts is {Length: >= 4});
            
            var surfaceTransform = rootTransform * Matrix4x4.TRS(LocalPosition, LocalRotation, Scale);

            return new FrustumPlane(
                bottomLeft: k_UnitPlaneVerts[k_UnitPlaneWinding[0]],
                bottomRight: k_UnitPlaneVerts[k_UnitPlaneWinding[1]],
                topLeft: k_UnitPlaneVerts[k_UnitPlaneWinding[3]],
                topRight: k_UnitPlaneVerts[k_UnitPlaneWinding[2]]
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