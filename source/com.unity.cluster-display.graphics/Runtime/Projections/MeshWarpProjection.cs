using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
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

        public Vector2Int ScreenResolution => m_ScreenResolution;

        public Vector3 Position => m_Position;

        public Vector3 Rotation => m_Rotation;

        public Vector3 Scale => m_Scale;

        public Mesh Mesh => m_Mesh;

        public Bounds CalculateBounds(Matrix4x4 origin)
        {
            var localToWorld = origin * Matrix4x4.TRS(Position, Quaternion.Euler(Rotation), Scale);
            var bounds = new Bounds(localToWorld.MultiplyPoint(m_Mesh.bounds.center), Vector3.zero);
            foreach (var corner in m_Mesh.bounds.Corners())
            {
                bounds.Encapsulate(localToWorld.MultiplyPoint(corner));
            }

            return bounds;
        }
    }

    [PopupItem("Mesh Warp")]
    [CreateAssetMenu(fileName = "Mesh Warp Projection",
        menuName = "Cluster Display/Mesh Warp Projection")]
    sealed class MeshWarpProjection : ProjectionPolicy
    {
        public enum OuterFrustumMode
        {
            RealtimeCubemap,
            StaticCubemap,
            SolidColor
        }

        [SerializeField]
        List<MeshData> m_Meshes = new();
        [SerializeField]
        Vector3 m_OuterViewPosition;
        [SerializeField]
        int m_OuterFrustumCubemapSize = 512;
        [SerializeField]
        Color m_BackgroundColor = Color.black;

        [SerializeField]
        Cubemap m_StaticCubemap;

        [SerializeField]
        bool m_RenderInnerOuterFrustum;

        [SerializeField]
        OuterFrustumMode m_OuterFrustumMode;

        // RTs holding the results of the normal "flat" renders
        readonly Dictionary<int, RenderTexture> m_MainRenderTargets = new();

        Material m_WarpMaterial;

        // Property blocks for the warp materials
        readonly Dictionary<int, MaterialPropertyBlock> m_WarpMaterialProperties = new();

        // RTs holding the warped renders (to be blitted as the final output)
        readonly Dictionary<int, RenderTexture> m_RenderTargets = new();

        // Frustums of the "flat" renders
        readonly Dictionary<int, SlicedFrustumGizmo> m_FrustumGizmos = new();

        RenderTexture m_OuterFrustumTarget;
        Cubemap m_BlankBackground;

        BlitCommand m_BlitCommand;

        // Property blocks for preview materials
        Material m_PreviewMaterial;
        readonly Dictionary<int, MaterialPropertyBlock> m_PreviewMaterialProperties = new();

        int[] m_NodesToRender;

        static readonly int k_MainTex = Shader.PropertyToID("_MainTex");
        static readonly int k_CameraTransform = Shader.PropertyToID("_CameraTransform");
        static readonly int k_CameraProjection = Shader.PropertyToID("_CameraProjection");
        static readonly int k_OuterFrustum = Shader.PropertyToID("_BackgroundTex");
        static readonly int k_BackgroundColor = Shader.PropertyToID("_BackgroundColor");
        static readonly int k_OuterFrustumCenter = Shader.PropertyToID("_OuterFrustumCenter");

        void OnValidate()
        {
            m_NodesToRender = IsDebug ? Enumerable.Range(0, m_Meshes.Count).ToArray() : new[] {GetEffectiveNodeIndex()};
        }

        void OnEnable()
        {
            m_WarpMaterial = new Material(Shader.Find(GraphicsUtil.k_WarpShaderName));
            m_PreviewMaterial = new Material(Shader.Find("Unlit/Texture"));
            m_BlankBackground = new Cubemap(1, GraphicsUtil.GetGraphicsFormat(), TextureCreationFlags.None);
            m_BlankBackground.SetPixel(CubemapFace.NegativeX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveZ, 0, 0, Color.white);
        }

        public override void OnDisable()
        {
            m_MainRenderTargets.Clean();
            m_RenderTargets.Clean();

            if (m_OuterFrustumTarget != null)
            {
                m_OuterFrustumTarget.Release();
                m_OuterFrustumTarget = null;
            }

            DestroyImmediate(m_WarpMaterial);
            DestroyImmediate(m_PreviewMaterial);
            DestroyImmediate(m_BlankBackground);
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            if (m_Meshes.Count == 0) return;

            var nodeIndex = GetEffectiveNodeIndex();
            if (!Application.isEditor)
            {
                m_NodesToRender ??= new[] {nodeIndex};
            }

            if (!IsDebug && nodeIndex >= m_Meshes.Count)
            {
                return;
            }

            var cubeMapCenter = Origin.MultiplyPoint(m_OuterViewPosition);
            if (m_RenderInnerOuterFrustum && m_OuterFrustumMode == OuterFrustumMode.RealtimeCubemap)
            {
                if (GraphicsUtil.AllocateIfNeeded(ref m_OuterFrustumTarget, m_OuterFrustumCubemapSize, m_OuterFrustumCubemapSize))
                {
                    m_OuterFrustumTarget.dimension = TextureDimension.Cube;
                    m_OuterFrustumTarget.name = "Outer frustum Render Target";
                }

                // TODO: Check when m_OuterViewPosition or the mesh changes and calculate which face of the cube map we need to
                // render.  One approach could be to render a cube map with Red, Green and Blue faces on the geometry and
                // seeing which color are present on the mesh.
                using var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None);
                cameraScope.RenderToCubemap(m_OuterFrustumTarget, cubeMapCenter);

                // Enable code below to dump the cubemap to disk and make debugging easier
                // GraphicsUtil.SaveCubemapToFile(m_OuterFrustumTarget, "c:\\temp\\cubemap.png");
            }

            Texture outerFrustumCubemap = m_OuterFrustumMode switch
            {
                OuterFrustumMode.RealtimeCubemap => m_OuterFrustumTarget,
                OuterFrustumMode.StaticCubemap => m_StaticCubemap,
                OuterFrustumMode.SolidColor => m_BlankBackground,
                _ => throw new ArgumentOutOfRangeException()
            };

            var backgroundColor = m_OuterFrustumMode switch {
                OuterFrustumMode.RealtimeCubemap or OuterFrustumMode.StaticCubemap => Color.white,
                OuterFrustumMode.SolidColor => m_BackgroundColor,
                _ => throw new ArgumentOutOfRangeException()
            };

            m_WarpMaterial.SetTexture(k_OuterFrustum, outerFrustumCubemap);
            m_WarpMaterial.SetColor(k_BackgroundColor, backgroundColor);
            m_WarpMaterial.SetVector(k_OuterFrustumCenter, cubeMapCenter);

            foreach (var index in m_NodesToRender)
            {
                // TODO: increase camera FOV for overscan
                var meshData = m_Meshes[index];
                if (!meshData.Mesh || meshData.ScreenResolution.sqrMagnitude == 0 || meshData.Scale.sqrMagnitude == 0)
                {
                    Debug.LogWarning("Malformed mesh description");
                    break;
                }

                var mainRenderTarget = m_MainRenderTargets.GetOrAllocate(index,
                    meshData.ScreenResolution,
                    "Main Render");

                var meshBounds = meshData.CalculateBounds(Origin);

                Matrix4x4 projectionOverride = activeCamera.projectionMatrix;
                Quaternion rotationOverride = activeCamera.transform.rotation;
                Matrix4x4 worldToCameraOverride = activeCamera.worldToCameraMatrix;

                if (!m_RenderInnerOuterFrustum)
                {
                    (projectionOverride, worldToCameraOverride, rotationOverride) =
                        activeCamera.GetBoundingOverrides(meshBounds);
                }

                var worldToProjection = projectionOverride * worldToCameraOverride;

                var gizmo = m_FrustumGizmos.GetOrCreate(index);
                gizmo.ViewProjectionInverse = worldToProjection.inverse;
                gizmo.GridSize = Vector2Int.one;

                if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(worldToProjection), meshBounds))
                {
                    using var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None);
                    cameraScope.Render(mainRenderTarget, projectionOverride,
                        null, null, null,
                        rotationOverride);
                }

                var prop = m_WarpMaterialProperties.GetOrCreate(index);

                prop.SetTexture(k_MainTex, mainRenderTarget);
                prop.SetMatrix(k_CameraTransform, worldToCameraOverride);
                prop.SetMatrix(k_CameraProjection, projectionOverride);

                RenderMesh(meshData, clusterSettings, m_WarpMaterial, prop, activeCamera,
                    m_RenderTargets.GetOrAllocate(index, meshData.ScreenResolution, "Warp"));
            }

            if (IsDebug)
            {
                for (var index = 0; index < m_Meshes.Count; index++)
                {
                    var mesh = m_Meshes[index];
                    var prop = m_PreviewMaterialProperties.GetOrCreate(index);
                    prop.SetTexture(k_MainTex, m_RenderTargets.GetOrAllocate(index, mesh.ScreenResolution));
                    RenderMesh(mesh, clusterSettings, m_PreviewMaterial, prop);
                }
            }

            if (m_RenderTargets.TryGetValue(nodeIndex, out var warpResult))
            {
                m_BlitCommand = new BlitCommand(
                    warpResult,
                    new BlitParams(
                            m_Meshes[nodeIndex].ScreenResolution,
                            clusterSettings.OverScanInPixels, Vector2.zero)
                        .ScaleBias,
                    GraphicsUtil.k_IdentityScaleBias,
                    customBlitMaterial,
                    GetCustomBlitMaterialPropertyBlocks(nodeIndex));
            }
        }

        public override void OnDrawGizmos()
        {
            foreach (var frustumGizmo in m_FrustumGizmos.Values)
            {
                frustumGizmo.Draw();
            }
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
            MaterialPropertyBlock propertyBlock = null,
            Camera activeCamera = null, RenderTexture target = null)
        {
            var localToWorld = Origin * Matrix4x4.TRS(mesh.Position, Quaternion.Euler(mesh.Rotation), mesh.Scale);
            UnityEngine.Graphics.DrawMesh(mesh.Mesh,
                localToWorld,
                material,
                ClusterRenderer.ProjectionSurfaceLayer,
                activeCamera,
                submeshIndex: 0,
                properties: propertyBlock,
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
    }

    static class MeshProjectionExtensions
    {
        public static RenderTexture GetOrAllocate(this Dictionary<int, RenderTexture> renderTextures, int index,
            Vector2Int resolution, string name = "")
        {
            renderTextures.TryGetValue(index, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                resolution.x,
                resolution.y))
            {
                rt.name = $"RT {name} {index}";
                renderTextures[index] = rt;
            }

            return rt;
        }

        public static void Clean(this Dictionary<int, RenderTexture> renderTextures)
        {
            foreach (var rt in renderTextures.Values)
            {
                if (rt)
                {
                    rt.Release();
                }
            }

            renderTextures.Clear();
        }

        public static TValue GetOrCreate<TValue, TKey>(this Dictionary<TKey, TValue> dictionary, TKey index) where TValue : new()
        {
            if (!dictionary.TryGetValue(index, out var item))
            {
                item = new();
                dictionary.Add(index, item);
            }

            return item;
        }

        public static IEnumerable<Vector3> Corners(this Bounds bounds) =>
            new[]
            {
                bounds.min, bounds.max,
                new(bounds.min.x, bounds.min.y, bounds.max.z),
                new(bounds.min.x, bounds.max.y, bounds.max.z),
                new(bounds.min.x, bounds.max.y, bounds.min.z),
                new(bounds.max.x, bounds.max.y, bounds.min.z),
                new(bounds.max.x, bounds.min.y, bounds.min.z),
                new(bounds.max.x, bounds.min.y, bounds.max.z),
            };

        static Matrix4x4 GetBoundingProjection(this IEnumerable<Vector3> vertices, Matrix4x4 worldToCamera,
            float zNear, float zFar)
        {
            float maxSlopeX = 0;
            float maxSlopeY = 0;
            foreach (var t in vertices)
            {
                var p = worldToCamera.MultiplyPoint(t);
                var slopeX = Mathf.Abs(p.x / p.z);
                if (slopeX > maxSlopeX)
                {
                    maxSlopeX = slopeX;
                }

                var slopeY = Mathf.Abs(p.y / p.z);
                if (slopeY > maxSlopeY)
                {
                    maxSlopeY = slopeY;
                }
            }

            var aspect = maxSlopeX / maxSlopeY;
            var fieldOfView = Mathf.Atan(maxSlopeY) * 2 * Mathf.Rad2Deg;
            return Matrix4x4.Perspective(fieldOfView, aspect, zNear, zFar);
        }

        public static (Matrix4x4 projection, Matrix4x4 worldToCamera, Quaternion rotation) GetBoundingOverrides(
            this Camera camera,
            Bounds bounds)
        {
            var transform = camera.transform;
            var position = transform.position;
            var up = transform.up;

            var lookDir = bounds.center - position;
            var rotation = Quaternion.LookRotation(lookDir, up);

            // Note that cameraToWorld follows the OpenGL convention, which is right-handed and -Z forward
            var scale = camera.transform.localScale;
            scale.z = -scale.z;
            var worldToCamera = Matrix4x4.TRS(position, rotation, scale).inverse;
            var projection = bounds.Corners().GetBoundingProjection(
                worldToCamera,
                camera.nearClipPlane,
                camera.farClipPlane);

            return (projection, worldToCamera, rotation);
        }
    }
}
