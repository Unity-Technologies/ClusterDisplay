using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    public class TrackedPerspectiveSurface : MonoBehaviour
    {
#if CLUSTER_DISPLAY_HDRP
        const string k_ShaderName = "HDRP/Unlit";
#elif CLUSTER_DISPLAY_URP
        const string k_ShaderName = "Universal Render Pipeline/Unlit";
#endif

        /// <summary>
        /// The output resolution of the screen.
        /// </summary>
        [SerializeField]
        Vector2Int m_ScreenResolution = new Vector2Int(1920, 1080);

        MeshRenderer m_Renderer;
        MeshFilter m_MeshFilter;
        Material m_ScreenPreviewMaterial;
        RenderTexture m_RenderTarget;
        RenderTexture m_PreviewTexture;

        GraphicsFormat m_GraphicsFormat;

        int[] m_CornerIndices;
        int m_ScreenIndex;

        public RenderTexture RenderTarget => m_RenderTarget;
        public Vector2Int Resolution => m_ScreenResolution;

        void OnEnable()
        {
            gameObject.layer = ClusterRenderer.VirtualObjectLayer;
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            Initialize();
        }

        void OnValidate()
        {
            Initialize();
        }

        void Initialize()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_MeshFilter = GetComponent<MeshFilter>();
            Assert.IsNotNull(m_MeshFilter);
            m_CornerIndices = GetMeshCorners(m_MeshFilter.sharedMesh);

            if (m_ScreenPreviewMaterial == null)
            {
                m_ScreenPreviewMaterial = new Material(Shader.Find(k_ShaderName));
                m_Renderer.material = m_ScreenPreviewMaterial;
            }
        }

        public void Render(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var overscannedSize = m_ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

            GraphicsUtil.AllocateIfNeeded(
                ref m_RenderTarget,
                overscannedSize.x,
                overscannedSize.y,
                m_GraphicsFormat);

            var mesh = m_MeshFilter.sharedMesh;
            var cornersView = new Vector3[m_CornerIndices.Length];
            var cornersWorld = new Vector3[m_CornerIndices.Length];

            for (var i = 0; i < m_CornerIndices.Length; i++)
            {
                cornersWorld[i] = transform.TransformPoint(mesh.vertices[m_CornerIndices[i]]);
            }

            var cameraTransform = activeCamera.transform;
            var savedRotation = cameraTransform.rotation;
            var position = cameraTransform.position;
            var lookAtPoint = ProjectPointToPlane(position, cornersWorld);

            Debug.DrawLine(position, lookAtPoint);
            var upDir = cornersWorld[2] - cornersWorld[0];
            activeCamera.transform.LookAt(lookAtPoint, upDir);

            for (var i = 0; i < m_CornerIndices.Length; i++)
            {
                cornersView[i] = cameraTransform.InverseTransformPoint(cornersWorld[i]);
            }

            var projectionMatrix = GetProjectionMatrix(activeCamera.projectionMatrix, cornersView, m_ScreenResolution, clusterSettings.OverScanInPixels);

            using var cameraScope = new CameraScope(activeCamera);
            cameraScope.Render(projectionMatrix, m_RenderTarget);
            cameraTransform.rotation = savedRotation;

#if UNITY_EDITOR
            if (GraphicsUtil.AllocateIfNeeded(
                ref m_PreviewTexture,
                m_ScreenResolution.x,
                m_ScreenResolution.y,
                m_GraphicsFormat))
            {
                m_ScreenPreviewMaterial.mainTexture = m_PreviewTexture;
            }

            UnityEngine.Graphics.Blit(
                source: m_RenderTarget, dest: m_PreviewTexture,
                scale: (Vector2)m_ScreenResolution / overscannedSize,
                offset: Vector2.one * clusterSettings.OverScanInPixels / overscannedSize);
#endif
        }

        static int[] GetMeshCorners(Mesh mesh)
        {
            var cornerIndices = new int[4] {0, 0, 0, 0};
            for (var index = 0; index < mesh.uv.Length; index++)
            {
                if (Mathf.Approximately(mesh.uv[index].x, 0))
                {
                    if (Mathf.Approximately(mesh.uv[index].y, 0))
                    {
                        cornerIndices[0] = index;
                    }
                    else if (Mathf.Approximately(mesh.uv[index].y, 1))
                    {
                        cornerIndices[2] = index;
                    }
                }
                else if (Mathf.Approximately(mesh.uv[index].x, 1))
                {
                    if (Mathf.Approximately(mesh.uv[index].y, 0))
                    {
                        cornerIndices[1] = index;
                    }
                    else if (Mathf.Approximately(mesh.uv[index].y, 1))
                    {
                        cornerIndices[3] = index;
                    }
                }
            }

            return cornerIndices;
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

#if UNITY_EDITOR
        public static TrackedPerspectiveSurface CreateDefaultPlanar(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = GameObjectUtility.GetUniqueNameForSibling(parent, "Cluster Screen");
            var surface = go.AddComponent<TrackedPerspectiveSurface>();
            var aspect = (float)surface.Resolution.x / surface.Resolution.y;
            var scale = go.transform.localScale;
            scale.z /= aspect;
            scale /= 2;
            go.transform.localScale = scale;
            if (ClusterCameraManager.Instance.ActiveCamera is { } activeCamera)
            {
                var camTransform = activeCamera.transform;
                var position = camTransform.position;
                go.transform.position = position + camTransform.forward * 3f;
                go.transform.rotation = camTransform.rotation;
                go.transform.Rotate(90, 180, 0);
            }
            else
            {
                go.transform.Rotate(Vector3.left, -90);
            }

            go.transform.SetParent(parent, worldPositionStays: true);
            return surface;
        }
#endif
    }
}
