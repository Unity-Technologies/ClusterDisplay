using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace Unity.ClusterDisplay.Graphics
{
    static class MeshUtils
    {
        public enum UVRotation
        {
            Zero,
            CW90,
            CW180,
            CW270
        }

        public static void RotateUVs(this Mesh mesh, UVRotation rotation)
        {
            Matrix3x2 r2;
            var r = rotation switch
            {
                UVRotation.Zero => new Matrix2x2(1, 0, 0, 1),
                UVRotation.CW90 => new Matrix2x2(0, -1, 1, 0),
                UVRotation.CW180 => new Matrix2x2(-1, 0, 0, -1),
                UVRotation.CW270 => new Matrix2x2(0, 1, -1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };
            var t = rotation switch {
                UVRotation.Zero => new Vector2(0, 0),
                UVRotation.CW90 => new Vector2(1, 0),
                UVRotation.CW180 => new Vector2(1, 1),
                UVRotation.CW270 => new Vector2(0, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null)
            };

            var uvs = mesh.uv;

            for (int i = 0; i < uvs.Length; i++)
            {
                var uv = uvs[i];
                uvs[i] = r.MultiplyVector(uv) + t;
            }

            mesh.uv = uvs;
        }

        struct Matrix2x2
        {
            public float m00;
            public float m01;
            public float m10;
            public float m11;

            public Matrix2x2(float m00, float m01, float m10, float m11)
            {
                this.m00 = m00;
                this.m01 = m01;
                this.m10 = m10;
                this.m11 = m11;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector2 MultiplyVector(this Matrix2x2 m, Vector2 v) =>
            new(m.m00 * v.x + m.m01 * v.y, m.m10 * v.x + m.m11 * v.y);
    }
}
