using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;


namespace Unity.ClusterDisplay.Graphics
{
    [PopupItem("Tracked Perspective")]
    public sealed class TrackedPerspectiveProjection : ProjectionPolicy
    {
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

            UpdatePreview(clusterSettings);
        }

        public override void Present(CommandBuffer commandBuffer)
        {
            if (m_ProjectionSurfaces.Count == 0 || m_BlitCommand.texture == null)
            {
                return;
            }

            GraphicsUtil.Blit(commandBuffer, m_BlitCommand);
        }

        // public override Matrix4x4 Origin
        // {
        //     get => m_Origin;
        //     set
        //     {
        //         m_Origin = value;
        //         foreach (var surface in m_ProjectionSurfaces)
        //         {
        //             surface.RootTransform = value;
        //         }
        //     }
        // }

        public void UpdatePreview(ClusterRendererSettings clusterSettings)
        {
            foreach (var preview in m_ActivePreviews)
            {
                m_PreviewPool.Release(preview);
            }

            m_ActivePreviews.Clear();

            if (m_IsDebug)
            {
                for (var index = 0; index < m_ProjectionSurfaces.Count; index++)
                {
                    var surface = m_ProjectionSurfaces[index];
                    var preview = m_PreviewPool.Get();
                    m_ActivePreviews.Add(preview);

                    preview.Draw(Origin.MultiplyPoint(surface.LocalPosition),
                        Origin.rotation * surface.LocalRotation,
                        surface.Scale,
                        m_RenderTargets[index],
                        surface.ScreenResolution,
                        clusterSettings.OverScanInPixels,
                        m_GraphicsFormat);
                }
            }
        }

        public void AddSurface()
        {
            m_ProjectionSurfaces.Add(ProjectionSurface.CreateDefaultPlanar($"Screen {m_ProjectionSurfaces.Count}"));
        }

        public void RemoveSurface(int index)
        {
            m_ProjectionSurfaces.RemoveAt(index);
            m_RenderTargets.Remove(index);
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

            var cornersWorld = surface.GetVertices(Origin);
            var cornersView = new Vector3[cornersWorld.Length];

            var cameraTransform = activeCamera.transform;
            var savedRotation = cameraTransform.rotation;
            var position = cameraTransform.position;
            var lookAtPoint = ProjectPointToPlane(position, cornersWorld);

            Debug.DrawLine(position, lookAtPoint);
            var upDir = cornersWorld[2] - cornersWorld[0];
            activeCamera.transform.LookAt(lookAtPoint, upDir);

            for (var i = 0; i < cornersView.Length; i++)
            {
                cornersView[i] = cameraTransform.InverseTransformPoint(cornersWorld[i]);
            }

            var projectionMatrix = GetProjectionMatrix(activeCamera.projectionMatrix,
                cornersView,
                surface.ScreenResolution,
                clusterSettings.OverScanInPixels);

            using var cameraScope = new CameraScope(activeCamera);
            cameraScope.Render(projectionMatrix, GetRenderTexture(index, overscannedSize));
            cameraTransform.rotation = savedRotation;
        }

        static Vector3 ProjectPointToPlane(Vector3 pt, Vector3[] plane)
        {
            var normal = Vector3.Cross(plane[1] - plane[0], plane[2] - plane[0]).normalized;
            return pt - Vector3.Dot(pt - plane[0], normal) * normal;
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
    }
}