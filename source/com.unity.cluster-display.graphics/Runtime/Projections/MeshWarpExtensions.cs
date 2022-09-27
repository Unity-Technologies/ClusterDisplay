using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    static class MeshWarpExtensions
    {
        /// <summary>
        /// Retrieves (allocating if needed) a <see cref="RenderTexture"/> from a dictionary with
        /// the specified key and resolution.
        /// </summary>
        /// <param name="renderTextures">A dictionary of <see cref="RenderTexture"/>s.</param>
        /// <param name="key">The key that identifies the desired texture.</param>
        /// <param name="resolution">The desired resolution in pixels.</param>
        /// <param name="name">Name assigned to the texture, if allocating.</param>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <returns>A valid <see cref="RenderTexture"/> with the desired resolution.</returns>
        public static RenderTexture GetOrAllocate<TKey>(this Dictionary<TKey, RenderTexture> renderTextures, TKey key,
            Vector2Int resolution, string name = "")
        {
            renderTextures.TryGetValue(key, out var rt);

            if (GraphicsUtil.AllocateIfNeeded(
                ref rt,
                resolution.x,
                resolution.y))
            {
                rt.name = $"{name}:{key}";
                renderTextures[key] = rt;
            }

            return rt;
        }

        /// <summary>
        /// Releases all <see cref="RenderTexture"/>s in the dictionary and clears it.
        /// </summary>
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

        /// <summary>
        /// Retrieves an item from the dictionary. If the item does not exist, it will be created using the default
        /// constructor and added to the dictionary.
        /// </summary>
        public static bool GetOrCreate<TValue, TKey>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue item) where TValue : new()
        {
            var found = dictionary.TryGetValue(key, out item);
            if (!found)
            {
                item = new TValue();
                dictionary.Add(key, item);
            }

            return !found;
        }

        /// <summary>
        /// Fills a collection with the 8 corners of a <see cref="Bounds"/> struct.
        /// </summary>
        /// <param name="bounds">The bounds.</param>
        /// <param name="corners">Array that will contain the output corners. Must contain at least 8 elements.</param>
        public static void GetCorners(this Bounds bounds, Span<Vector3> corners)
        {
            Assert.IsTrue(corners.Length >= 8);

            corners[0] = bounds.min;
            corners[1] = bounds.max;
            corners[2] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            corners[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
            corners[4] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            corners[5] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
            corners[6] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            corners[7] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        }

        /// <summary>
        /// Computes a perspective projection matrix that can encompass the given vertices.
        /// </summary>
        /// <param name="vertices">A collection of vertices in world space.</param>
        /// <param name="worldToCamera">Matrix that transforms from world to camera space.</param>
        /// <param name="zNear">The near clipping plane.</param>
        /// <param name="zFar">The far clipping plane value.</param>
        /// <returns>An asymmetric perspective projection that can view all the vertices.</returns>
        /// <remarks>
        /// The result is undefined if any vertices are behind the camera. Assumes that the camera
        /// axis is contained within the frustum planes.
        /// </remarks>
        static Matrix4x4 GetBoundingProjection(this Span<Vector3> vertices, Matrix4x4 worldToCamera,
            float zNear, float zFar)
        {
            float maxSlopeX = 0;
            float maxSlopeY = 0;
            float minSlopeX = 0;
            float minSlopeY = 0;

            foreach (var t in vertices)
            {
                var p = worldToCamera.MultiplyPoint(t);
                var slopeX = p.x / -p.z;
                maxSlopeX = Math.Max(maxSlopeX, slopeX);
                minSlopeX = Math.Min(minSlopeX, slopeX);

                var slopeY = p.y / -p.z;
                maxSlopeY = Math.Max(maxSlopeY, slopeY);
                minSlopeY = Math.Min(minSlopeY, slopeY);
            }

            var frustumPlanes = new FrustumPlanes
            {
                zNear = zNear,
                zFar = zFar,
                left = minSlopeX * zNear,
                right = maxSlopeX * zNear,
                top = maxSlopeY * zNear,
                bottom = minSlopeY * zNear
            };

            return Matrix4x4.Frustum(frustumPlanes);
        }

        /// <summary>
        /// Computes a projection matrix that applies the given amount of overscan.
        /// </summary>
        /// <param name="projection">The original projection matrix.</param>
        /// <param name="resolution">The original resolution.</param>
        /// <param name="overscanPixels">The amount of overscan in pixels.</param>
        /// <returns>A new projection matrix that will give the desired overscan.</returns>
        static Matrix4x4 GetOverscanProjection(this Matrix4x4 projection, Vector2Int resolution, int overscanPixels)
        {
            var frustumPlanes = projection.decomposeProjection;
            var overscanMultiplier = Vector2.one + Vector2.one * overscanPixels / resolution;
            frustumPlanes.left *= overscanMultiplier.x;
            frustumPlanes.right *= overscanMultiplier.x;
            frustumPlanes.top *= overscanMultiplier.y;
            frustumPlanes.bottom *= overscanMultiplier.y;
            return Matrix4x4.Frustum(frustumPlanes);
        }

        /// <summary>
        /// Computes camera parameters that, when applied, will make the given bounds completely visible.
        /// </summary>
        /// <param name="camera">The camera for which to compute the parameters.</param>
        /// <param name="bounds">The bounds that should be made visible.</param>
        /// <returns>The overridden projection, world to camera matrix, and rotation.</returns>
        /// <remarks>
        /// This method computes overrides for projection and rotation only (assumes that the position is locked).
        /// </remarks>
        static (Matrix4x4 projection, Matrix4x4 worldToCamera, Quaternion rotation) GetBoundingOverrides(
            this Camera camera,
            Bounds bounds)
        {
            var transform = camera.transform;
            var position = transform.position;
            // TODO: we can compute "up" more intelligently to minimize the FOV that we need to render.

            var lookDir = bounds.center - position;
            var rotation = Quaternion.LookRotation(lookDir, transform.up);

            // We need to account for the fact that worldToCamera follows the OpenGL convention,
            // which is right-handed and -Z forward, while Unity's transforms are left-handed and +Z forward.
            var scale = transform.localScale;
            scale.z = -scale.z;
            var worldToCamera = Matrix4x4.TRS(position, rotation, scale).inverse;
            Span<Vector3> corners = stackalloc Vector3[8];
            bounds.GetCorners(corners);
            var projection = corners.GetBoundingProjection(
                worldToCamera,
                camera.nearClipPlane,
                camera.farClipPlane);

            return (projection, worldToCamera, rotation);
        }

        /// <summary>
        /// Computes camera parameters to include overscan and, optionally, to ensure a given mesh
        /// (defined by its bounds) is visible in its entirety
        /// </summary>
        /// <remarks>
        /// We can use the <paramref name="bounds"/> argument render projection that can "fill up" an entire mesh.
        /// </remarks>
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

        /// <summary>
        /// Renders a cubemap to the specified <see cref="RenderTexture"/>.
        /// </summary>
        /// <param name="activeCamera">The camera to be used rendering.</param>
        /// <param name="renderTarget">The <see cref="RenderTexture"/> to store the output.</param>
        /// <param name="size">The size of the cubemap (in pixels).</param>
        /// <param name="cubeMapCenter">The position in world space to render the cubemap from.</param>
        public static void RenderRealtimeCubemap(this Camera activeCamera,
            ref RenderTexture renderTarget,
            int size,
            Vector3 cubeMapCenter)
        {
            if (GraphicsUtil.AllocateIfNeeded(ref renderTarget, size, size))
            {
                renderTarget.dimension = TextureDimension.Cube;
                renderTarget.name = "Outer frustum Render Target";
            }

            // TODO: Check when m_OuterViewPosition or the mesh changes and calculate which face of the cube map we need to
            // render.  One approach could be to render a cube map with Red, Green and Blue faces on the geometry and
            // seeing which color are present on the mesh.
            using var cameraScope = CameraScopeFactory.Create(activeCamera, RenderFeature.ClearHistory);
            cameraScope.RenderToCubemap(renderTarget, cubeMapCenter);

            // Enable code below to dump the cubemap to disk and make debugging easier
            // GraphicsUtil.SaveCubemapToFile(m_OuterFrustumTarget, "c:\\temp\\cubemap.png");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidResolution(this Vector2Int resolution) => resolution.x > 0 && resolution.y > 0;
    }
}
