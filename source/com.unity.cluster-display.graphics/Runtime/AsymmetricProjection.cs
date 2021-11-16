using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    struct AsymmetricProjection
    {
        readonly Matrix4x4 m_OriginalProjection;

        public AsymmetricProjection(Matrix4x4 originalProjection)
        {
            m_OriginalProjection = originalProjection;
        }

        public Matrix4x4 GetFrustumSlice(Rect normalizedViewportSubsection)
        {
            var baseFrustumPlanes = m_OriginalProjection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes();
            frustumPlanes.zNear = baseFrustumPlanes.zNear;
            frustumPlanes.zFar = baseFrustumPlanes.zFar;
            frustumPlanes.left = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMin);
            frustumPlanes.right = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMax);
            frustumPlanes.bottom = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMin);
            frustumPlanes.top = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMax);
            return Matrix4x4.Frustum(frustumPlanes);
        }
    }
}
