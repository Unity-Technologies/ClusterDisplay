using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Graphics
{
    [Serializable]
    class TrackedPerspectiveSurface
    {
#if CLUSTER_DISPLAY_HDRP
        const string k_ShaderName = "HDRP/Unlit";
#elif CLUSTER_DISPLAY_URP
        const string k_ShaderName = "Universal Render Pipeline/Unlit";
#endif
        [SerializeField]
        string m_Name = "Screen";

        /// <summary>
        /// The output resolution of the screen.
        /// </summary>
        [SerializeField]
        Vector2Int m_ScreenResolution = new Vector2Int(1920, 1080);

        [SerializeField]
        Vector2 m_PhysicalSize = new Vector2(4.8f, 2.7f);

        [SerializeField]
        Vector3 m_LocalPosition = Vector3.zero;
        [SerializeField]
        Quaternion m_LocalRotation = Quaternion.identity;
        
        RenderTexture m_RenderTarget;

        public bool DrawPreview { get; set; } = true;
        
        public Matrix4x4 RootTransform { get; internal set; } = Matrix4x4.identity;

        public GraphicsFormat GraphicsFormat { get; private set; }
        
        static readonly Quaternion k_BasePlaneRotation = Quaternion.Euler(90, 0, 0);
        static readonly Vector3 k_BaseScale = Vector3.one / 10f;

        static readonly Vector3[] k_PlaneCorners =
        {
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };

        public RenderTexture RenderTarget => m_RenderTarget;
        public Vector2Int Resolution => m_ScreenResolution;

        public Vector3 Scale => new Vector3(m_PhysicalSize.x, m_PhysicalSize.y, 1);

        public Quaternion Rotation
        {
            get => RootTransform.rotation * m_LocalRotation;
            internal set => m_LocalRotation = (value * RootTransform.inverse.rotation).normalized;
        }

        public Vector3 Position
        {
            get => RootTransform.MultiplyPoint(m_LocalPosition);
            set => m_LocalPosition = RootTransform.inverse.MultiplyPoint(value);
        }

        internal Vector3[] GetVertices()
        {
            var surfaceTransform = Matrix4x4.TRS(Position, Rotation, Scale);

            var cornersWorld = new Vector3[k_PlaneCorners.Length];
            for (var i = 0; i < k_PlaneCorners.Length; i++)
            {
                cornersWorld[i] = surfaceTransform.MultiplyPoint(k_PlaneCorners[i]);
            }

            return cornersWorld;
        }

        public void Render(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            if (GraphicsFormat == GraphicsFormat.None)
            {
                GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            }

            var overscannedSize = m_ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;
            var scale = new Vector3(m_PhysicalSize.x, m_PhysicalSize.y, 1);

            GraphicsUtil.AllocateIfNeeded(
                ref m_RenderTarget,
                overscannedSize.x,
                overscannedSize.y,
                GraphicsFormat);

            var cornersView = new Vector3[k_PlaneCorners.Length];
            var cornersWorld = GetVertices();

            var cameraTransform = activeCamera.transform;
            var savedRotation = cameraTransform.rotation;
            var position = cameraTransform.position;
            var lookAtPoint = ProjectPointToPlane(position, cornersWorld);

            Debug.DrawLine(position, lookAtPoint);
            var upDir = cornersWorld[2] - cornersWorld[0];
            activeCamera.transform.LookAt(lookAtPoint, upDir);

            for (var i = 0; i < k_PlaneCorners.Length; i++)
            {
                cornersView[i] = cameraTransform.InverseTransformPoint(cornersWorld[i]);
            }

            var projectionMatrix = GetProjectionMatrix(activeCamera.projectionMatrix, cornersView, m_ScreenResolution, clusterSettings.OverScanInPixels);

            using var cameraScope = new CameraScope(activeCamera);
            cameraScope.Render(projectionMatrix, m_RenderTarget);
            cameraTransform.rotation = savedRotation;

            if (DrawPreview)
            {
                PerspectiveSurfacePreview.UpdateSurfacePreview(this, clusterSettings);
            }
        }

        static Matrix4x4 GetProjectionMatrix(
            Matrix4x4 originalProjection,
            IList<Vector3> planeCorners,
            Vector2Int resolution,
            int overScanInPixels)
        {
            var planeLeft = planeCorners[0].x;
            var planeRight = planeCorners[1].x;
            var planeDepth = planeCorners[0].z;
            var planeTop = planeCorners[2].y;
            var planeBottom = planeCorners[0].y;
            var originalFrustum = originalProjection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes
            {
                zNear = originalFrustum.zNear,
                zFar = originalFrustum.zFar,
                left = planeLeft * originalFrustum.zNear / planeDepth,
                right = planeRight * originalFrustum.zNear / planeDepth,
                top = planeTop * originalFrustum.zNear / planeDepth,
                bottom = planeBottom * originalFrustum.zNear / planeDepth
            };

            var frustumSize = new Vector2(
                frustumPlanes.right - frustumPlanes.left,
                frustumPlanes.top - frustumPlanes.bottom);
            var overscanDelta = frustumSize / resolution * overScanInPixels;
            frustumPlanes.left -= overscanDelta.x;
            frustumPlanes.right += overscanDelta.x;
            frustumPlanes.bottom -= overscanDelta.y;
            frustumPlanes.top += overscanDelta.y;

            return Matrix4x4.Frustum(frustumPlanes);
        }

        static Vector3 ProjectPointToPlane(Vector3 pt, Vector3[] plane)
        {
            var normal = Vector3.Cross(plane[1] - plane[0], plane[2] - plane[0]).normalized;
            return pt - Vector3.Dot(pt - plane[0], normal) * normal;
        }

        public static TrackedPerspectiveSurface CreateDefaultPlanar()
        {
            var surface = new TrackedPerspectiveSurface();

            surface.m_LocalPosition = Vector3.forward * 3f;
            surface.m_LocalRotation = Quaternion.Euler(0, 180, 0);

            return surface;
        }
    }
}
