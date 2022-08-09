using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        // RTs holding the results of the main "flat" renders
        readonly Dictionary<int, RenderTexture> m_MainRenderTargets = new();

        // Visualizes the frustums of the main renders
        readonly Dictionary<int, SlicedFrustumGizmo> m_FrustumGizmos = new();

        // Material that takes a flat render and reprojects in onto a mesh surface
        Material m_WarpMaterial;

        // Property blocks for the warp material
        readonly Dictionary<int, MaterialPropertyBlock> m_WarpMaterialProperties = new();

        // RTs containing the warped renders (to be blitted as the final output)
        readonly Dictionary<int, RenderTexture> m_RenderTargets = new();

        // RT holding the realtime cubemap
        RenderTexture m_OuterFrustumTarget;

        // Hardcoded cubemap with a single white pixel on each face
        Cubemap m_BlankBackground;

        // Command for the final present
        BlitCommand m_BlitCommand;

        // Material for drawing the warped renders onto the meshes for preview purposes
        Material m_PreviewMaterial;

        // Property blocks for preview materials
        readonly Dictionary<int, MaterialPropertyBlock> m_PreviewMaterialProperties = new();

        // The node indices to render. Typically this is an array of 1 determined
        // by the cluster's ID assignment.
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
            m_FrustumGizmos.Clear();
        }

        void OnEnable()
        {
            m_WarpMaterial = new Material(Shader.Find(GraphicsUtil.k_WarpShaderName));
            m_PreviewMaterial = new Material(Shader.Find("Unlit/Transparent"));
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
            var nodeIndex = GetEffectiveNodeIndex();

            if (m_Meshes.Count == 0 || nodeIndex >= m_Meshes.Count) return;

            if (!Application.isEditor)
            {
                m_NodesToRender ??= new[] {nodeIndex};
            }

            if (m_RenderInnerOuterFrustum)
            {
                var cubeMapCenter = Origin.MultiplyPoint(m_OuterViewPosition);
                if (m_OuterFrustumMode == OuterFrustumMode.RealtimeCubemap)
                {
                    activeCamera.RenderRealtimeCubemap(ref m_OuterFrustumTarget, m_OuterFrustumCubemapSize, cubeMapCenter);
                }

                ConfigureOuterFrustumRendering(cubeMapCenter);
            }

            foreach (var index in m_NodesToRender)
            {
                var meshData = m_Meshes[index];
                if (!meshData.Mesh || meshData.ScreenResolution.sqrMagnitude == 0 || meshData.Scale.sqrMagnitude == 0)
                {
                    // TODO: provide this warning in the UI
                    Debug.LogWarning("Malformed mesh description");
                    break;
                }

                // Overscan is applied to the main render.
                var overscannedResolution = meshData.ScreenResolution + Vector2Int.one * clusterSettings.OverScanInPixels;
                var mainRenderTarget = m_MainRenderTargets.GetOrAllocate(index,
                    overscannedResolution,
                    "Main Render");

                // Calculate the camera settings for the main render.
                // These can differ from activeCamera's original settings due to overscan
                // or the requirement to completely fill in the mesh surface.
                var meshBounds = meshData.CalculateBounds(Origin);

                var cameraOverrides = activeCamera.ComputeSettingsForMainRender(meshData.ScreenResolution,
                    clusterSettings.OverScanInPixels,
                    m_RenderInnerOuterFrustum ? null : meshBounds);

                var worldToProjection = cameraOverrides.projection * cameraOverrides.worldToCamera;

                // Perform the main render.
                // We only need to do this if the mesh is visible to the main render.
                // For instance, if the camera is facing away from the mesh,
                // then we can skip the main render entirely.
                if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(worldToProjection), meshBounds))
                {
                    using var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None);
                    cameraScope.Render(mainRenderTarget, cameraOverrides.projection,
                        null, null, null,
                        cameraOverrides.rotation);
                }

                // Update frustum visualization
                if (m_FrustumGizmos.GetOrCreate(index, out var gizmo))
                {
                    gizmo.GridSize = Vector2Int.one;
                }

                gizmo.ViewProjectionInverse = worldToProjection.inverse;

                // Perform the warping
                var localToWorld = Origin * Matrix4x4.TRS(meshData.Position, Quaternion.Euler(meshData.Rotation), meshData.Scale);
                m_WarpMaterialProperties.GetOrCreate(index, out var prop);
                prop.SetTexture(k_MainTex, mainRenderTarget);
                prop.SetMatrix(k_CameraTransform, cameraOverrides.worldToCamera);
                prop.SetMatrix(k_CameraProjection, cameraOverrides.projection);

                var warpRenderTarget = m_RenderTargets.GetOrAllocate(index, meshData.ScreenResolution, "Warp");
                meshData.Draw(localToWorld, m_WarpMaterial, prop, activeCamera, warpRenderTarget);

                if (IsDebug)
                {
                    // Apply the warp result to the mesh for debug visualization. Since no camera
                    // is specified, it will be rendered by any active camera. When Cluster Renderer
                    // is enabled, the only "active" camera should be the SceneView.
                    m_PreviewMaterialProperties.GetOrCreate(index, out var previewMatProp);
                    previewMatProp.SetTexture(k_MainTex, warpRenderTarget);
                    meshData.Draw(localToWorld, m_PreviewMaterial, previewMatProp);
                }
            }

            // Prepare to blit the warp render target to the screen.
            if (m_RenderTargets.TryGetValue(nodeIndex, out var warpResult))
            {
                // Since the warping operation already accounts for overscan, we don't
                // need to specify any overscan for the final blit to screen.
                m_BlitCommand = new BlitCommand(
                    warpResult,
                    new BlitParams(
                            m_Meshes[nodeIndex].ScreenResolution,
                            0, Vector2.zero)
                        .ScaleBias,
                    GraphicsUtil.k_IdentityScaleBias,
                    customBlitMaterial,
                    GetCustomBlitMaterialPropertyBlocks(nodeIndex));
            }
        }

        void ConfigureOuterFrustumRendering(Vector3 cubeMapCenter)
        {
            Texture outerFrustumCubemap;
            var backgroundColor = Color.white;
            switch (m_OuterFrustumMode)
            {
                case OuterFrustumMode.RealtimeCubemap:
                    outerFrustumCubemap = m_OuterFrustumTarget;
                    break;
                case OuterFrustumMode.StaticCubemap:
                    outerFrustumCubemap = m_StaticCubemap;
                    break;
                case OuterFrustumMode.SolidColor:
                    outerFrustumCubemap = m_BlankBackground;
                    backgroundColor = m_BackgroundColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            m_WarpMaterial.SetTexture(k_OuterFrustum, outerFrustumCubemap);
            m_WarpMaterial.SetColor(k_BackgroundColor, backgroundColor);
            m_WarpMaterial.SetVector(k_OuterFrustumCenter, cubeMapCenter);
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
    }
}
