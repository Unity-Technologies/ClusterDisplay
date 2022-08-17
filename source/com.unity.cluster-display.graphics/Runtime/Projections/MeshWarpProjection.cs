using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Holds information about a mesh surface
    /// </summary>
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
            Span<Vector3> corners = stackalloc Vector3[8];
            m_Mesh.bounds.GetCorners(corners);
            foreach (var corner in corners)
            {
                bounds.Encapsulate(localToWorld.MultiplyPoint(corner));
            }

            return bounds;
        }
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
    [CreateAssetMenu(fileName = "Mesh Warp Projection",
        menuName = "Cluster Display/Mesh Warp Projection")]
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
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _CameraTransform = Shader.PropertyToID("_CameraTransform");
            public static readonly int _CameraProjection = Shader.PropertyToID("_CameraProjection");
            public static readonly int _BackgroundTex = Shader.PropertyToID("_BackgroundTex");
            public static readonly int _BackgroundColor = Shader.PropertyToID("_BackgroundColor");
            public static readonly int _OuterFrustumCenter = Shader.PropertyToID("_OuterFrustumCenter");
        }

        const string k_PreviewShaderName = "Unlit/Transparent";
        [AlwaysIncludeShader]
        const string k_WarpShaderName = "Hidden/ClusterDisplay/MeshWarp";

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

        readonly Queue<DrawMeshCommand> m_WarpCommands = new();

        // The node indices to render. Typically this is an array of 1 determined
        // by the cluster's ID assignment.
        int[] m_NodesToRender;

        void OnValidate()
        {
            m_NodesToRender = IsDebug ? Enumerable.Range(0, m_Meshes.Count).ToArray() : new[] {GetEffectiveNodeIndex()};
            m_FrustumGizmos.Clear();
        }

        public override void OnEnable()
        {
            m_WarpMaterial = GraphicsUtil.CreateHiddenMaterial(k_WarpShaderName);
            m_PreviewMaterial = GraphicsUtil.CreateHiddenMaterial(k_PreviewShaderName);

            m_BlankBackground = new Cubemap(1, GraphicsUtil.GetGraphicsFormat(), TextureCreationFlags.None);
            m_BlankBackground.SetPixel(CubemapFace.NegativeX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveX, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveY, 0, 0, Color.white);
            m_BlankBackground.SetPixel(CubemapFace.PositiveZ, 0, 0, Color.white);
            m_BlankBackground.Apply();
        }

        public override void OnDisable()
        {
            m_MainRenderTargets.Clean();
            m_RenderTargets.Clean();

            GraphicsUtil.DeallocateIfNeeded(ref m_OuterFrustumTarget);

            DestroyImmediate(m_WarpMaterial);
            DestroyImmediate(m_PreviewMaterial);
            DestroyImmediate(m_BlankBackground);
        }

        public override void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera)
        {
            var nodeIndex = GetEffectiveNodeIndex();

            if (m_Meshes.Count == 0 || (!IsDebug && nodeIndex >= m_Meshes.Count)) return;

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

                // FIXME: We currently need to disable the history if we're rendering per-frame cubemaps
                // because this interferes with motion blur, TAA, and other temporal operations.
                // One way to fix this is to render the cubemap from another camera.
                var renderFeatureFlags = IsDebug ||
                    m_RenderInnerOuterFrustum && m_OuterFrustumMode == OuterFrustumMode.RealtimeCubemap
                        ? RenderFeature.ClearHistory
                        : RenderFeature.None;

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
                var localToWorld = Origin * Matrix4x4.TRS(meshData.Position, Quaternion.Euler(meshData.Rotation), meshData.Scale);
                m_WarpMaterialProperties.GetOrCreate(index, out var prop);
                prop.SetTexture(ShaderIDs._MainTex, mainRenderTarget);
                prop.SetMatrix(ShaderIDs._CameraTransform, cameraOverrides.worldToCamera);
                prop.SetMatrix(ShaderIDs._CameraProjection, cameraOverrides.projection);

                m_WarpCommands.Enqueue(new DrawMeshCommand
                {
                    Mesh = meshData.Mesh,
                    LocalToWorldMatrix = localToWorld,
                    Material = m_WarpMaterial,
                    PropertyBlock = prop,
                    Target = m_RenderTargets.GetOrAllocate(index, meshData.ScreenResolution, "Warp")
                });
            }

            if (IsDebug)
            {
                foreach (var index in m_NodesToRender)
                {
                    // Apply the warp result to the mesh for debug visualization. Since no camera
                    // is specified, it will be rendered by any active camera. When Cluster Renderer
                    // is enabled, the only "active" camera should be the SceneView.
                    m_PreviewMaterialProperties.GetOrCreate(index, out var previewMatProp);
                    previewMatProp.SetTexture(ShaderIDs._MainTex, m_RenderTargets[index]);
                    var meshData = m_Meshes[index];
                    var localToWorld = Origin * Matrix4x4.TRS(meshData.Position, Quaternion.Euler(meshData.Rotation), meshData.Scale);
                    UnityEngine.Graphics.DrawMesh(meshData.Mesh,
                        localToWorld,
                        m_PreviewMaterial,
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

            m_WarpMaterial.SetTexture(ShaderIDs._BackgroundTex, outerFrustumCubemap);
            m_WarpMaterial.SetColor(ShaderIDs._BackgroundColor, backgroundColor);
            m_WarpMaterial.SetVector(ShaderIDs._OuterFrustumCenter, cubeMapCenter);
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
