using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    static class MeshWarpExtensions
    {
        public static RenderTexture GetOrAllocate<TKey>(this Dictionary<TKey, RenderTexture> renderTextures, TKey key,
            Vector2Int resolution, string name = "")
        {
            renderTextures.TryGetValue(key, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                resolution.x,
                resolution.y))
            {
                rt.name = $"RT {name} {key}";
                renderTextures[key] = rt;
            }

            return rt;
        }

        public static void Clean<TKey>(this Dictionary<TKey, RenderTexture> renderTextures)
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

        public static bool GetOrCreate<TValue, TKey>(this Dictionary<TKey, TValue> dictionary, TKey index, out TValue item) where TValue : new()
        {
            var found = dictionary.TryGetValue(index, out item);
            if (!found)
            {
                item = new TValue();
                dictionary.Add(index, item);
            }

            return !found;
        }

        public static IEnumerable<Vector3> Corners(this Bounds bounds) =>
            new[] {bounds.min, bounds.max,
                new(bounds.min.x, bounds.min.y, bounds.max.z),
                new(bounds.min.x, bounds.max.y, bounds.max.z),
                new(bounds.min.x, bounds.max.y, bounds.min.z),
                new(bounds.max.x, bounds.max.y, bounds.min.z),
                new(bounds.max.x, bounds.min.y, bounds.min.z),
                new(bounds.max.x, bounds.min.y, bounds.max.z),};

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

        static Matrix4x4 GetOverscanProjection(this Matrix4x4 projection, Vector2Int resolution, int overscanPixels)
        {
            if (overscanPixels <= 0)
            {
                return projection;
            }

            var frustumPlanes = projection.decomposeProjection;
            var overscanMultiplier = Vector2.one + Vector2.one * overscanPixels / resolution;
            frustumPlanes.left *= overscanMultiplier.x;
            frustumPlanes.right *= overscanMultiplier.x;
            frustumPlanes.top *= overscanMultiplier.y;
            frustumPlanes.bottom *= overscanMultiplier.y;
            return Matrix4x4.Frustum(frustumPlanes);
        }

        static (Matrix4x4 projection, Matrix4x4 worldToCamera, Quaternion rotation) GetBoundingOverrides(
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

        public static (Matrix4x4 projection, Matrix4x4 worldToCamera, Quaternion rotation) ComputeSettingsForMainRender(
            this Camera camera,
            Vector2Int resolution,
            int overscanPixels,
            Bounds? bounds = null)
        {
            var projection = camera.projectionMatrix;
            var rotation = camera.transform.rotation;
            var worldToCamera = camera.worldToCameraMatrix;

            if (bounds.HasValue)
            {
                (projection, worldToCamera, rotation) = camera.GetBoundingOverrides(bounds.Value);
            }

            projection = projection.GetOverscanProjection(resolution, overscanPixels);

            return (projection, worldToCamera, rotation);
        }

        public static void RenderRealtimeCubemap(this Camera activeCamera, ref RenderTexture renderTarget, int size, Vector3 cubeMapCenter)
        {
            if (GraphicsUtil.AllocateIfNeeded(ref renderTarget, size, size))
            {
                renderTarget.dimension = TextureDimension.Cube;
                renderTarget.name = "Outer frustum Render Target";
            }

            // TODO: Check when m_OuterViewPosition or the mesh changes and calculate which face of the cube map we need to
            // render.  One approach could be to render a cube map with Red, Green and Blue faces on the geometry and
            // seeing which color are present on the mesh.
            using var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.None);
            cameraScope.RenderToCubemap(renderTarget, cubeMapCenter);

            // Enable code below to dump the cubemap to disk and make debugging easier
            // GraphicsUtil.SaveCubemapToFile(m_OuterFrustumTarget, "c:\\temp\\cubemap.png");
        }

        public static void Draw(this MeshData mesh, Matrix4x4 localToWorld, Material material,
            MaterialPropertyBlock propertyBlock = null,
            Camera activeCamera = null, RenderTexture target = null)
        {
            // TODO: merge ProjectionSurfaceLayer with VirtualObjectLayer
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
                using var scope = CameraScopeFactory.Create(activeCamera, RenderFeature.None);

                // Point the camera at the mesh to make sure the mesh isn't culled.
                // Note that the transform component will be ignored by the warp shader (we don't
                // use the built-in camera transform uniforms).
                var meshCenter = localToWorld.MultiplyPoint(mesh.Mesh.bounds.center);
                activeCamera.transform.LookAt(meshCenter, Vector3.up);

                // Render just the mesh (ignore the scene)
                activeCamera.cullingMask = 1 << ClusterRenderer.ProjectionSurfaceLayer;

                scope.Render(target, null);
            }
        }
    }
}
