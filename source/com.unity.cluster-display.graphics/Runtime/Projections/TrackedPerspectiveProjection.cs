using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    [PopupItem("Tracked Perspective")]
    [CreateAssetMenu(fileName = "TrackedPerspectiveProjection",
        menuName = "Cluster Display/Tracked Perspective Projection",
        order = 2)]
    public sealed class TrackedPerspectiveProjection : ProjectionPolicy
    {
        // TODO: Create a custom icon for this.
        const string k_SurfaceIconName = "d_BuildSettings.Standalone.Small";
        
        [SerializeField]
        bool m_IsDebug;

        [SerializeField]
        List<ProjectionSurface> m_ProjectionSurfaces = new();

        [SerializeField]
        int m_NodeIndexOverride;

        readonly Dictionary<int, RenderTexture> m_RenderTargets = new();
        Camera m_Camera;
        BlitCommand m_BlitCommand;

        readonly UnityEngine.Pool.ObjectPool<ProjectionPreview> m_PreviewPool =
            new(
                createFunc: ProjectionPreview.Create,
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => DestroyImmediate(p.gameObject)
            );

        readonly List<ProjectionPreview> m_ActivePreviews = new();

        public IReadOnlyList<ProjectionSurface> Surfaces => m_ProjectionSurfaces;

        GraphicsFormat m_GraphicsFormat;

        void OnEnable()
        {
            m_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        }

        void OnDisable()
        {
            ClearPreviews();
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var nodeIndex = m_IsDebug || !ClusterSync.Active ? m_NodeIndexOverride : ClusterSync.Instance.DynamicLocalNodeId;

            if (nodeIndex >= m_ProjectionSurfaces.Count)
            {
                return;
            }

            if (m_IsDebug)
            {
                for (var index = 0; index < m_ProjectionSurfaces.Count; index++)
                {
                    RenderSurface(index, clusterSettings, activeCamera);
                }
            }
            else
            {
                RenderSurface(nodeIndex, clusterSettings, activeCamera);
            }

            m_BlitCommand = new BlitCommand(
                m_RenderTargets[nodeIndex],
                new BlitParams(
                        m_ProjectionSurfaces[nodeIndex].ScreenResolution,
                        clusterSettings.OverScanInPixels, Vector2.zero)
                    .ScaleBias,
                GraphicsUtil.k_IdentityScaleBias);
            
            ClearPreviews();
            if (m_IsDebug)
            {
                DrawPreview(clusterSettings);
            }
        }

        public override void Present(CommandBuffer commandBuffer)
        {
            if (m_ProjectionSurfaces.Count == 0 || m_BlitCommand.texture == null)
            {
                return;
            }

            GraphicsUtil.Blit(commandBuffer, m_BlitCommand);
        }

        void ClearPreviews()
        {
            foreach (var preview in m_ActivePreviews)
            {
                m_PreviewPool.Release(preview);
            }

            m_ActivePreviews.Clear();
        }

        void DrawPreview(ClusterRendererSettings clusterSettings)
        {
            for (var index = 0; index < m_ProjectionSurfaces.Count; index++)
            {
                if (m_RenderTargets.TryGetValue(index, out var rt))
                {
                    var surface = m_ProjectionSurfaces[index];
                    var preview = m_PreviewPool.Get();
                    m_ActivePreviews.Add(preview);

                    preview.Draw(Origin.MultiplyPoint(surface.LocalPosition),
                        Origin.rotation * surface.LocalRotation,
                        surface.Scale,
                        rt,
                        surface.ScreenResolution,
                        clusterSettings.OverScanInPixels,
                        m_GraphicsFormat);
                }
            }
        }

        public override void DrawGizmos(ClusterRendererSettings clusterSettings)
        {
#if UNITY_EDITOR
            foreach (var surface in Surfaces)
            {
                Gizmos.DrawIcon(Origin.MultiplyPoint(surface.LocalPosition), k_SurfaceIconName);
            }
#endif
        }
        
        public void AddSurface()
        {
            m_ProjectionSurfaces.Add(ProjectionSurface.CreateDefaultPlanar($"Screen {m_ProjectionSurfaces.Count}"));
        }

        public void RemoveSurface(int index)
        {
            m_ProjectionSurfaces.RemoveAt(index);

            if (m_RenderTargets.TryGetValue(index, out var rt))
            {
                rt.Release();
                m_RenderTargets.Remove(index);
            }
        }

        public void SetSurface(int index, ProjectionSurface surface)
        {
            Assert.IsTrue(index < m_ProjectionSurfaces.Count);
            m_ProjectionSurfaces[index] = surface;
        }

        RenderTexture GetRenderTexture(int index, Vector2Int overscannedSize)
        {
            m_RenderTargets.TryGetValue(index, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                overscannedSize.x,
                overscannedSize.y,
                m_GraphicsFormat))
            {
                m_RenderTargets[index] = rt;
            }

            return rt;
        }

        void RenderSurface(int index,
            ClusterRendererSettings clusterSettings,
            Camera activeCamera)
        {
            var surface = m_ProjectionSurfaces[index];
            var overscannedSize = surface.ScreenResolution + clusterSettings.OverScanInPixels * 2 * Vector2Int.one;

            var surfacePlane = surface.GetFrustumPlane(Origin);

            var cameraTransform = activeCamera.transform;
            var savedRotation = cameraTransform.rotation;
            var position = cameraTransform.position;

            var lookAtPoint = ProjectPointToPlane(position, surfacePlane);

            if (m_IsDebug)
            {
                Debug.DrawLine(position, lookAtPoint);
            }

            var upDir = surfacePlane.TopLeft - surfacePlane.BottomLeft;
            activeCamera.transform.LookAt(lookAtPoint, upDir);

            var planeInViewCoords = surfacePlane.ApplyTransform(cameraTransform.worldToLocalMatrix);

            var projectionMatrix = GetProjectionMatrix(activeCamera.projectionMatrix,
                planeInViewCoords,
                surface.ScreenResolution,
                clusterSettings.OverScanInPixels);

            using var cameraScope = new CameraScope(activeCamera);
            cameraScope.Render(projectionMatrix, GetRenderTexture(index, overscannedSize));
            cameraTransform.rotation = savedRotation;
        }

        static Vector3 ProjectPointToPlane(Vector3 pt, in ProjectionSurface.FrustumPlane plane)
        {
            var normal = Vector3.Cross(plane.BottomRight - plane.BottomLeft,
                    plane.TopLeft - plane.BottomLeft)
                .normalized;
            return pt - Vector3.Dot(pt - plane.BottomLeft, normal) * normal;
        }

        static Matrix4x4 GetProjectionMatrix(
            Matrix4x4 originalProjection,
            in ProjectionSurface.FrustumPlane plane,
            Vector2Int resolution,
            int overScanInPixels)
        {
            var planeLeft = plane.BottomLeft.x;
            var planeRight = plane.BottomRight.x;
            var planeDepth = plane.BottomLeft.z;
            var planeTop = plane.TopLeft.y;
            var planeBottom = plane.BottomLeft.y;
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
    }
}
