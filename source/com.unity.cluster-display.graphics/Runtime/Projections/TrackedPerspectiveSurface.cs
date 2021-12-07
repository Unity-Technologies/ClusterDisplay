using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    [Serializable]
    class TrackedPerspectiveSurface : IDisposable
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
    //
    // [SerializeField]
    // string m_Name = "Projection Surface";

    [SerializeField]
    Vector3 m_Position = Vector3.zero;
    [SerializeField]
    Quaternion m_Rotation = Quaternion.identity;
    [SerializeField]
    Vector3 m_Scale = 0.5f * Vector3.one;

    MeshRenderer m_PreviewRenderer;
    Material m_ScreenPreviewMaterial;
    RenderTexture m_RenderTarget;
    RenderTexture m_PreviewTexture;

    GraphicsFormat m_GraphicsFormat;

    static readonly Vector3[] k_PlaneCorners =
    {
        new Vector3(5, 0, 5),
        new Vector3(-5, 0, 5),
        new Vector3(5, 0, -5),
        new Vector3(-5, 0, -5)
    };

    public RenderTexture RenderTarget => m_RenderTarget;
    public Vector2Int Resolution => m_ScreenResolution;

    public TrackedPerspectiveSurface()
    {
    }

    void InitializePreview()
    {
        GameObject previewObject = null;
        if (m_PreviewRenderer == null)
        {
            previewObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            previewObject.layer = ClusterRenderer.VirtualObjectLayer;
            previewObject.hideFlags = HideFlags.HideInHierarchy;
            m_PreviewRenderer = previewObject.GetComponent<MeshRenderer>();
        }

        if (m_ScreenPreviewMaterial == null)
        {
            m_ScreenPreviewMaterial = new Material(Shader.Find(k_ShaderName));
            m_PreviewRenderer.material = m_ScreenPreviewMaterial;
        }
    }

    public void Enable()
    {
        if (m_PreviewRenderer)
        {
            m_PreviewRenderer.gameObject.SetActive(true);
        }
    }
    
    public void Disable()
    {
        if (m_PreviewRenderer)
        {
            m_PreviewRenderer.gameObject.SetActive(false);
        }
    }

    ~TrackedPerspectiveSurface()
    {
        DestroyEditorGameObjects();
    }

    public void Render(ClusterRendererSettings clusterSettings, Camera activeCamera, Matrix4x4 rootTransform)
    {
        if (m_GraphicsFormat == GraphicsFormat.None)
        {
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        }
        
        var overscannedSize = m_ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;
        var surfaceTransform = rootTransform * Matrix4x4.TRS(m_Position, m_Rotation, m_Scale);

        GraphicsUtil.AllocateIfNeeded(
            ref m_RenderTarget,
            overscannedSize.x,
            overscannedSize.y,
            m_GraphicsFormat);

        var cornersView = new Vector3[k_PlaneCorners.Length];
        var cornersWorld = new Vector3[k_PlaneCorners.Length];

        for (var i = 0; i < k_PlaneCorners.Length; i++)
        {
            cornersWorld[i] = surfaceTransform.MultiplyPoint(k_PlaneCorners[i]);
        }

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

#if UNITY_EDITOR
        if (m_PreviewRenderer == null)
        {
            InitializePreview();
        }
        
        var previewTransform = m_PreviewRenderer.transform;
        previewTransform.position = m_Position;
        previewTransform.rotation = m_Rotation;
        previewTransform.localScale = m_Scale;
        
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
            scale: (Vector2) m_ScreenResolution / overscannedSize,
            offset: Vector2.one * clusterSettings.OverScanInPixels / overscannedSize);
#endif
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
    public static TrackedPerspectiveSurface CreateDefaultPlanar()
    {
        // var go = new GameObject("Cluster Screen", typeof(TrackedPerspectiveSurface));
        // go.name = GameObjectUtility.GetUniqueNameForSibling(null, "Cluster Screen");
        var surface = new TrackedPerspectiveSurface();
        
        var aspect = (float)surface.Resolution.x / surface.Resolution.y;
        
        var scale = surface.m_Scale;
        scale.z /= aspect;
        scale /= 2;
        surface.m_Scale = scale;
        
        if (ClusterCameraManager.Instance.ActiveCamera is { } activeCamera)
        {
            var camTransform = activeCamera.transform;
            var position = camTransform.position;
            surface.m_Position = position + camTransform.forward * 3f;
            surface.m_Rotation = camTransform.rotation;
            surface.m_Rotation *= Quaternion.Euler(90, 180, 0);
        }
        else
        {
            // go.transform.Rotate(Vector3.left, -90);
            surface.m_Rotation = Quaternion.AngleAxis(-90, Vector3.left);
        }

        // go.transform.SetParent(parent, worldPositionStays: true);
        // go.hideFlags = HideFlags.HideInHierarchy;
        return surface;
    }
#endif
        void DestroyEditorGameObjects()
        {
            if (m_PreviewRenderer != null)
            {
                Object.DestroyImmediate(m_PreviewRenderer.gameObject);
            }
        }

        public void Dispose()
        {
            DestroyEditorGameObjects();
            GC.SuppressFinalize(this);
        }
    }
}
