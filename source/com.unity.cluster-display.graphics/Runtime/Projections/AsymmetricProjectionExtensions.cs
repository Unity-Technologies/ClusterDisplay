using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class AsymmetricProjectionExtensions
    {
        public static Matrix4x4 GetFrustumSlice(ref this Matrix4x4 projection, Rect normalizedViewportSubsection)
        {
            var baseFrustumPlanes = projection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes
            {
                zNear = baseFrustumPlanes.zNear,
                zFar = baseFrustumPlanes.zFar,
                left = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMin),
                right = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMax),
                bottom = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMin),
                top = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMax)
            };
            return Matrix4x4.Frustum(frustumPlanes);
        }
    }
}
