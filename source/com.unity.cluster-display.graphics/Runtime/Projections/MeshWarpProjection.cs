using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.Assertions;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Unity.ClusterDisplay.Graphics
{
    [Serializable]
    class MeshData
    {
        [SerializeField]
        Mesh m_Mesh;

        [SerializeField]
        Vector2Int m_ScreenResolution = new(1920, 1080);

        [SerializeField]
        Vector3 m_Position;

        [SerializeField]
        Vector3 m_Rotation;

        [SerializeField]
        Vector3 m_Scale = Vector3.one;

        public Vector2Int ScreenResolution
        {
            get => m_ScreenResolution;
            set => m_ScreenResolution = value;
        }

        public Vector3 Position
        {
            get => m_Position;
            set => m_Position = value;
        }

        public Vector3 Rotation
        {
            get => m_Rotation;
            set => m_Rotation = value;
        }

        public Vector3 Scale
        {
            get => m_Scale;
            set => m_Scale = value;
        }

        public Mesh Mesh
        {
            get => m_Mesh;
            set => m_Mesh = value;
        }
    }

    [PopupItem("Mesh Warp")]
    [CreateAssetMenu(fileName = "Mesh Warp Projection",
        menuName = "Cluster Display/Mesh Warp Projection")]
    sealed class MeshWarpProjection : ProjectionPolicy
    {
        [SerializeField]
        List<MeshData> m_Meshes = new();
        [SerializeField]
        Vector3 m_StagePosition;
        [SerializeField]
        int m_OuterFrustumCubemapSize = 512;

        readonly Dictionary<int, RenderTexture> m_RenderTargets = new();
        RenderTexture m_OuterFrustumTarget;

        BlitCommand m_BlitCommand;
        Material m_WarpMaterial;
        RenderTexture m_MainRenderTarget;
        GameObject m_TestCamera;

        readonly Dictionary<int, Material> m_PreviewMaterials = new();

        static readonly int k_CameraTransform = Shader.PropertyToID("_CameraTransform");
        static readonly int k_CameraProjection = Shader.PropertyToID("_CameraProjection");
        static readonly int k_OuterFrustum = Shader.PropertyToID("_OuterFrustum");
        static readonly int k_OuterFrustumCenter = Shader.PropertyToID("_OuterFrustumCenter");

        Vector2Int ChooseMainRenderTargetSize()
        {
            Vector2Int mainSize = default;
            foreach (var meshWarper in m_Meshes)
            {
                if (mainSize.sqrMagnitude < meshWarper.ScreenResolution.sqrMagnitude)
                {
                    mainSize = meshWarper.ScreenResolution;
                    break;
                }
            }

            return mainSize;
        }

        public override void OnDisable()
        {
            foreach (var rt in m_RenderTargets.Values)
            {
                if (rt != null)
                {
                    rt.Release();
                }
            }

            m_RenderTargets.Clear();

            if (m_OuterFrustumTarget != null)
            {
                m_OuterFrustumTarget.Release();
                m_OuterFrustumTarget = null;
            }

            DestroyImmediate(m_WarpMaterial);
            m_WarpMaterial = null;

            foreach (var previewMaterial in m_PreviewMaterials.Values)
            {
                DestroyImmediate(previewMaterial);
            }

            m_PreviewMaterials.Clear();
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var nodeIndex = GetEffectiveNodeIndex();

            if (m_Meshes.Count == 0) return;
            if (m_WarpMaterial == null)
            {
                m_WarpMaterial = new Material(Shader.Find(GraphicsUtil.k_WarpShaderName));
            }

            var mainRenderSize = ChooseMainRenderTargetSize();
            if (mainRenderSize.sqrMagnitude == 0)
            {
                return;
            }

            if (GraphicsUtil.AllocateIfNeeded(ref m_OuterFrustumTarget, m_OuterFrustumCubemapSize,
                    m_OuterFrustumCubemapSize))
            {
                m_OuterFrustumTarget.dimension = UnityEngine.Rendering.TextureDimension.Cube;
                m_OuterFrustumTarget.name = $"Outer frustum Render Target";
            }

            // TODO: Check when m_StagePosition or the mesh changes and calculate which face of the cube map we need to
            // render.  One approach could be to render a cube map with Red, Green and Blue faces on the geometry and
            // seeing which color are present on the mesh.
            using (var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None))
            {
                cameraScope.RenderToCubemap(m_OuterFrustumTarget, m_StagePosition);

                // Enable code below to dump the cubemap to disk and make debugging easier
                //GraphicsUtil.SaveCubemapToFile(m_OuterFrustumTarget, "c:\\temp\\cubemap.png");
            }

            // TODO: increase camera FOV for overscan
            using (var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None))
            {
                GraphicsUtil.AllocateIfNeeded(ref m_MainRenderTarget, mainRenderSize.x, mainRenderSize.y);
                m_MainRenderTarget.name = "Mesh Warp Main Render";
                cameraScope.Render(m_MainRenderTarget, null);
            }

            m_WarpMaterial.mainTexture = m_MainRenderTarget;
            m_WarpMaterial.SetMatrix(k_CameraTransform, activeCamera.worldToCameraMatrix);
            m_WarpMaterial.SetMatrix(k_CameraProjection, activeCamera.projectionMatrix);
            m_WarpMaterial.SetTexture(k_OuterFrustum, m_OuterFrustumTarget);
            m_WarpMaterial.SetVector(k_OuterFrustumCenter, m_StagePosition);

            if (IsDebug)
            {
                for (var index = 0; index < m_Meshes.Count; ++index)
                {
                    var meshData = m_Meshes[index];
                    RenderMesh(meshData, clusterSettings, m_WarpMaterial, activeCamera,
                        GetRenderTexture(index, meshData.ScreenResolution));
                }
            }
            else
            {
                if (nodeIndex >= m_Meshes.Count)
                {
                    return;
                }

                var meshData = m_Meshes[nodeIndex];

                Debug.Log($"RenderMesh {nodeIndex}");
                RenderMesh(meshData, clusterSettings, m_WarpMaterial, activeCamera,
                    GetRenderTexture(nodeIndex, meshData.ScreenResolution));
            }

            if (IsDebug)
            {
                for (var index = 0; index < m_Meshes.Count; ++index)
                {
                    var mesh = m_Meshes[index];
                    if (!m_PreviewMaterials.TryGetValue(index, out var material))
                    {
                        material = new Material(Shader.Find("Unlit/Texture"));
                        m_PreviewMaterials[index] = material;
                    }

                    material.mainTexture = GetRenderTexture(index, mesh.ScreenResolution);

                    RenderMesh(m_Meshes[index], clusterSettings, material, null, null);
                }
            }

            m_BlitCommand = new BlitCommand(
                m_RenderTargets[nodeIndex],
                new BlitParams(
                        m_Meshes[nodeIndex].ScreenResolution,
                        clusterSettings.OverScanInPixels, Vector2.zero)
                    .ScaleBias,
                GraphicsUtil.k_IdentityScaleBias,
                customBlitMaterial,
                GetCustomBlitMaterialPropertyBlocks(nodeIndex));
        }

        public override void Present(PresentArgs args)
        {
            if (m_Meshes.Count == 0 || m_BlitCommand.texture == null)
            {
                return;
            }

            GraphicsUtil.Blit(args.CommandBuffer, m_BlitCommand, args.FlipY);
        }

        void RenderMesh(MeshData mesh, ClusterRendererSettings clusterRendererSettings, Material material,
            Camera activeCamera = null, RenderTexture target = null)
        {
            if (!mesh.Mesh || mesh.ScreenResolution.sqrMagnitude == 0 || mesh.Scale.sqrMagnitude == 0)
            {
                Debug.LogWarning("Malformed mesh description");
                return;
            }

            var localToWorld = Origin * Matrix4x4.TRS(mesh.Position, Quaternion.Euler(mesh.Rotation), mesh.Scale);
            UnityEngine.Graphics.DrawMesh(mesh.Mesh,
                localToWorld,
                material,
                ClusterRenderer.ProjectionSurfaceLayer,
                activeCamera,
                submeshIndex: 0,
                properties: null,
                castShadows: false);

            if (activeCamera)
            {
                using var scope = CameraScopeFactory.Create(activeCamera, RenderFeature.AsymmetricProjection);

                // TODO: account for overscan
                // Make sure the mesh isn't culled by pointing the camera at it
                var meshCenter = localToWorld.MultiplyPoint(mesh.Mesh.bounds.center);
                activeCamera.transform.LookAt(meshCenter, Vector3.up);

                // Render just the mesh (ignore the scene)
                activeCamera.cullingMask = 1 << ClusterRenderer.ProjectionSurfaceLayer;

                scope.Render(target, null);
            }
        }


        RenderTexture GetRenderTexture(int index, Vector2Int resolution)
        {
            m_RenderTargets.TryGetValue(index, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                resolution.x,
                resolution.y))
            {
                rt.name = $"Cluster Render Target {index}";
                m_RenderTargets[index] = rt;
            }

            return rt;
        }
    }
}
