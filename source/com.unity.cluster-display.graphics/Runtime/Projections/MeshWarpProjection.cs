using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Holds information about a mesh surface
    /// </summary>
    [Serializable]
    class MeshProjectionSurface
    {
        [SerializeField]
        MeshRenderer m_MeshRenderer;

        [SerializeField]
        Vector2Int m_ScreenResolution = new(1920, 1080);

        public Vector2Int ScreenResolution => m_ScreenResolution;

        public MeshRenderer MeshRenderer => m_MeshRenderer;

        // Records the "enabled" state of the MeshRenderer so it can be disabled and restored
        // when performing the main render.
        public bool IsEnabled { get; set; }

        public bool IsValid => m_MeshRenderer != null && m_ScreenResolution.IsValidResolution();
    }

    /// <summary>
    /// Holds data needed to perform a mesh render.
    /// </summary>
    struct DrawMeshCommand
    {
        public Mesh Mesh;
        public Matrix4x4 LocalToWorldMatrix;
        public Material Material;
        public MaterialPropertyBlock PropertyBlock;
        public RenderTexture Target;
    }

    [PopupItem("Mesh Warp")]
    [RequiresUnreferencedShader]
    sealed class MeshWarpProjection : ProjectionPolicy
    {
        public enum OuterFrustumMode
        {
            /// <summary>
            /// Render the outer frustum with a cubemap. It is re-generated on each frame.
            /// </summary>
            RealtimeCubemap,
            /// <summary>
            /// Use a baked cubemap asset to render the outer frustum.
            /// </summary>
            StaticCubemap,
            /// <summary>
            /// Render the outer frustum with a solid color.
            /// </summary>
            SolidColor
        }

        static class ShaderIDs
        {
            public static readonly int _CameraTransform = Shader.PropertyToID("_CameraTransform");
            public static readonly int _CameraProjection = Shader.PropertyToID("_CameraProjection");
            public static readonly int _BackgroundTex = Shader.PropertyToID("_BackgroundTex");
            public static readonly int _BackgroundColor = Shader.PropertyToID("_BackgroundColor");
            public static readonly int _OuterFrustumCenter = Shader.PropertyToID("_OuterFrustumCenter");
        }

        const string k_PreviewShaderName = "Hidden/ClusterDisplay/ProjectionPreview";
        [AlwaysIncludeShader]
        const string k_WarpShaderName = "Hidden/ClusterDisplay/MeshWarp";

        [SerializeField]
        List<MeshProjectionSurface> m_ProjectionSurfaces = new();
        [SerializeField]
        Transform m_OuterViewPosition;
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

        // Cached Mesh objects belonging to the projection surfaces (to avoid a lookup each frame).
        readonly Dictionary<MeshProjectionSurface, Mesh> m_Meshes = new();

        // RT holding the realtime cubemap
        RenderTexture m_OuterFrustumTarget;

        // Hardcoded cubemap with a single white pixel on each face
        Cubemap m_BlankBackground;

        // Command for the final present
        BlitCommand m_BlitCommand;

        // Property blocks for preview materials
        readonly Dictionary<int, MaterialPropertyBlock> m_PreviewMaterialProperties = new();

        readonly Queue<DrawMeshCommand> m_WarpCommands = new();

        // The node indices to render. Typically this is an array of 1 determined
        // by the cluster's ID assignment.
        int[] m_NodesToRender;

        /// <summary>
        /// Scope object to disable and restore renderers on the projection surface objects.
        /// </summary>
        readonly struct HideSurfacesScope : IDisposable
        {
            IEnumerable<MeshProjectionSurface> Surfaces { get; }
            public HideSurfacesScope(IEnumerable<MeshProjectionSurface> surfaces)
            {
                Surfaces = surfaces;
                foreach (var surface in Surfaces)
                {
                    if (surface.MeshRenderer == null)
                    {
                        continue;
                    }
                    surface.IsEnabled = surface.MeshRenderer.enabled;
                    surface.MeshRenderer.enabled = false;
                }
            }

            public void Dispose()
            {
                foreach (var surface in Surfaces)
                {
                    if (surface.MeshRenderer == null)
                    {
                        continue;
                    }
                    surface.MeshRenderer.enabled = surface.IsEnabled;
                }
            }
        }

        public IReadOnlyList<MeshProjectionSurface> ProjectionSurfaces => m_ProjectionSurfaces;

        void OnValidate()
        {
            m_NodesToRender = IsDebug ? Enumerable.Range(0, ProjectionSurfaces.Count).ToArray() : new[] {GetEffectiveNodeIndex()};
            m_FrustumGizmos.Clear();

            m_Meshes.Clear();
            foreach (var surface in ProjectionSurfaces)
            {
                if (surface.MeshRenderer != null && surface.MeshRenderer.TryGetComponent<MeshFilter>(out var filter))
                {
                    m_Meshes.Add(surface, filter.sharedMesh);
                }
            }
        }

        public void AddSurface()
        {
            m_ProjectionSurfaces.Add(new MeshProjectionSurface());
        }

        void OnEnable()
        {
            m_WarpMaterial = GraphicsUtil.CreateHiddenMaterial(k_WarpShaderName);

            m_BlankBackground = new Cubemap(1, GraphicsUtil.GetGraphicsFormat(), 0);
            m_BlankBackground.SetPixel(CubemapFace.NegativeX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveZ, 0, 0, Color.white);
            m_BlankBackground.Apply();
        }

        public void OnDisable()
        {
            m_MainRenderTargets.Clean();
            m_RenderTargets.Clean();

            GraphicsUtil.DeallocateIfNeeded(ref m_OuterFrustumTarget);

            DestroyImmediate(m_WarpMaterial);
            DestroyImmediate(m_BlankBackground);
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var nodeIndex = GetEffectiveNodeIndex();

            if (ProjectionSurfaces.Count == 0 || (!IsDebug && nodeIndex >= ProjectionSurfaces.Count)) return;

            if (!Application.isEditor)
            {
                m_NodesToRender ??= new[] {nodeIndex};
            }

            // Hide the projection surfaces for performing the main render.
            using var hideScope = new HideSurfacesScope(ProjectionSurfaces);

            if (m_RenderInnerOuterFrustum)
            {
                var cubeMapCenter = m_OuterViewPosition != null ? m_OuterViewPosition.position : Origin.GetPosition();
                if (m_OuterFrustumMode == OuterFrustumMode.RealtimeCubemap)
                {
                    activeCamera.RenderRealtimeCubemap(ref m_OuterFrustumTarget, m_OuterFrustumCubemapSize, cubeMapCenter);
                }

                ConfigureOuterFrustumRendering(cubeMapCenter);
            }

            foreach (var index in m_NodesToRender)
            {
                var meshData = ProjectionSurfaces[index];
                if (meshData.MeshRenderer == null)
                {
                    continue;
                }

                var rendererEnabled = meshData.MeshRenderer.enabled;
                meshData.MeshRenderer.enabled = false;
                if (!meshData.IsValid)
                {
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
                var meshBounds = meshData.MeshRenderer.bounds;

                var cameraOverrides = activeCamera.ComputeSettingsForMainRender(meshData.ScreenResolution,
                    clusterSettings.OverScanInPixels,
                    m_RenderInnerOuterFrustum ? null : meshBounds);

                var worldToProjection = cameraOverrides.projection * cameraOverrides.worldToCamera;

                // FIXME: We currently need to disable the history if we're rendering per-frame cubemaps
                // because this interferes with motion blur, TAA, and other temporal operations.
                // One way to fix this is to render the cubemap from another camera.
                RenderFeature renderFeatureFlags = RenderFeature.AsymmetricProjection;
                if (IsDebug || m_RenderInnerOuterFrustum && m_OuterFrustumMode == OuterFrustumMode.RealtimeCubemap)
                {
                    renderFeatureFlags |= RenderFeature.ClearHistory;
                }

                // Perform the main render.
                // We only need to do this if the mesh is visible to the main render.
                // For instance, if the camera is facing away from the mesh,
                // then we can skip the main render entirely.
                if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(worldToProjection), meshBounds))
                {
                    using var cameraScope = CameraScopeFactory.Create(activeCamera, renderFeatureFlags);
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
                m_WarpMaterialProperties.GetOrCreate(index, out var prop);
                prop.SetTexture(GraphicsUtil.ShaderIDs._MainTex, mainRenderTarget);
                prop.SetMatrix(ShaderIDs._CameraTransform, cameraOverrides.worldToCamera);
                prop.SetMatrix(ShaderIDs._CameraProjection, cameraOverrides.projection);

                m_WarpCommands.Enqueue(new DrawMeshCommand
                {
                    Mesh = m_Meshes[meshData],
                    LocalToWorldMatrix = meshData.MeshRenderer.localToWorldMatrix,
                    Material = m_WarpMaterial,
                    PropertyBlock = prop,
                    Target = m_RenderTargets.GetOrAllocate(index, meshData.ScreenResolution, "Warp")
                });

                meshData.MeshRenderer.enabled = rendererEnabled;
            }

            if (IsDebug)
            {
                foreach (var index in m_NodesToRender)
                {
                    var meshData = ProjectionSurfaces[index];
                    if (!meshData.IsValid)
                    {
                        continue;
                    }

                    // Apply the warp result to the mesh for debug visualization. Since no camera
                    // is specified, it will be rendered by any active camera. When Cluster Renderer
                    // is enabled, the only "active" camera should be the SceneView.
                    m_PreviewMaterialProperties.GetOrCreate(index, out var previewMatProp);
                    previewMatProp.SetTexture(GraphicsUtil.ShaderIDs._MainTex, m_RenderTargets[index]);
                    var localToWorld = meshData.MeshRenderer.localToWorldMatrix;
                    UnityEngine.Graphics.DrawMesh(m_Meshes[meshData],
                        localToWorld,
                        GraphicsUtil.GetPreviewMaterial(),
                        ClusterRenderer.VirtualObjectLayer,
                        camera: null,
                        submeshIndex: 0,
                        properties: previewMatProp,
                        castShadows: false);
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
                            ProjectionSurfaces[nodeIndex].ScreenResolution,
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

            m_WarpMaterial.SetTexture(ShaderIDs._BackgroundTex, outerFrustumCubemap);
            m_WarpMaterial.SetColor(ShaderIDs._BackgroundColor, backgroundColor);
            m_WarpMaterial.SetVector(ShaderIDs._OuterFrustumCenter, cubeMapCenter);
        }

        public void OnDrawGizmos()
        {
            foreach (var frustumGizmo in m_FrustumGizmos.Values)
            {
                frustumGizmo.Draw();
            }
        }

        public override void Present(PresentArgs args)
        {
            if (ProjectionSurfaces.Count == 0 || m_BlitCommand.texture == null)
            {
                return;
            }

            var commandBuffer = args.CommandBuffer;
            while (m_WarpCommands.TryDequeue(out var meshCommand))
            {
                commandBuffer.SetRenderTarget(meshCommand.Target);
                commandBuffer.DrawMesh(meshCommand.Mesh,
                    meshCommand.LocalToWorldMatrix,
                    meshCommand.Material,
                    submeshIndex: 0,
                    shaderPass: 0,
                    meshCommand.PropertyBlock);
            }

            commandBuffer.SetRenderTarget(args.BackBuffer);
            GraphicsUtil.Blit(commandBuffer, m_BlitCommand, args.FlipY);
        }
    }
}
