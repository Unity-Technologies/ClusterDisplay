using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    static class GraphicsUtil
    {
        const string k_BlitShaderName = "ClusterDisplay/PresentBlit";
        static MaterialPropertyBlock s_PropertyBlock;
        static Material s_BlitMaterial;

        static MaterialPropertyBlock GetPropertyBlock()
        {
            if (s_PropertyBlock == null)
            {
                s_PropertyBlock = new MaterialPropertyBlock();
            }

            return s_PropertyBlock;
        }
        
        static Material GetBlitMaterial()
        {
            if (s_BlitMaterial == null)
            {
                var shader = Shader.Find(k_BlitShaderName);
                if (shader == null)
                {
                    // TODO we had a utility adding shader to the included list, bring it on.
                    throw new InvalidOperationException($"Could not find shader \"{k_BlitShaderName}\", " +
                        "make sure it has been added to the list of Always Included shaders");
                }

                s_BlitMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return s_BlitMaterial;
        }

        public static void Blit(CommandBuffer cmd, RenderTexture target, Vector4 texBias, Vector4 rtBias)
        {
            var propertyBlock = GetPropertyBlock();
            propertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), target);
            propertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            propertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            propertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), 0, MeshTopology.Quads, 4, 1, propertyBlock);
        }

        public static void Blit(CommandBuffer cmd, RTHandle target, Vector4 texBias, Vector4 rtBias)
        {
            var propertyBlock = GetPropertyBlock();
            propertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), target);
            propertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            propertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            propertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), 0, MeshTopology.Quads, 4, 1, propertyBlock);
        }
        
        public static bool AllocateIfNeeded(ref RenderTexture[] rts, int count, string name, int width, int height, GraphicsFormat format)
        {
            var changed = false;

            if (rts == null || count != rts.Length)
            {
                changed = true;
                DeallocateIfNeeded(ref rts);
                rts = new RenderTexture[count];
            }

            for (var i = 0; i != count; ++i)
            {
                // TODO name rarely used, inefficient
                // TODO we populate these arrays all at once,
                // we can assume all tex are similar, just check the 1st
                changed |= AllocateIfNeeded(ref rts[i], $"{name}-{i}", width, height, format);
            }

            return changed;
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, string name, int width, int height, GraphicsFormat format)
        {
            if (rt == null || 
                rt.width != width || 
                rt.height != height || 
                rt.graphicsFormat != format)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, 1, format, 0)
                {
                    name = $"{name}-{width}X{height}"
                };
                return true;
            }

            return false;
        }

        public static void DeallocateIfNeeded(ref RenderTexture[] rts)
        {
            if (rts == null)
            {
                return;
            }
            
            for (var i = 0; i != rts.Length; ++i)
            {
                DeallocateIfNeeded(ref rts[i]);
            }

            rts = null;
        }

        public static void DeallocateIfNeeded(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
            }

            rt = null;
        }

        public static bool AllocateIfNeeded(ref RTHandle[] rts, int count, string name, int width, int height)
        {
            var changed = false;

            if (rts == null || count != rts.Length)
            {
                changed = true;
                DeallocateIfNeeded(ref rts);
                rts = new RTHandle[count];
            }

            for (var i = 0; i != count; ++i)
            {
                // TODO name rarely used, inefficient
                // TODO we populate these arrays all at once,
                // we can assume all tex are similar, just check the 1st
                changed |= AllocateIfNeeded(ref rts[i], $"{name}-{i}", width, height);
            }

            return changed;
        }

        public static bool AllocateIfNeeded(ref RTHandle rt, string name, int width, int height)
        {
            if (rt == null ||
                rt.rt.width != width ||
                rt.rt.height != height)
            {
                if (rt != null)
                {
                    RTHandles.Release(rt);
                }

                rt = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    enableRandomWrite: true,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: $"{name}-({width}X{height})");

                return true;
            }

            return false;
        }

        public static void DeallocateIfNeeded(ref RTHandle[] rts)
        {
            if (rts == null)
            {
                return;
            }
            
            for (var i = 0; i != rts.Length; ++i)
            {
                DeallocateIfNeeded(ref rts[i]);
            }

            rts = null;
        }

        public static void DeallocateIfNeeded(ref RTHandle rt)
        {
            if (rt != null)
            {
                RTHandles.Release(rt);
            }

            rt = null;
        }

        public static Matrix4x4 GetClusterDisplayParams(Rect overscannedViewportSubsection, Vector2 globalScreenSize, Vector2Int gridSize)
        {
            var parms = new Matrix4x4();

            var translationAndScale = new Vector4(overscannedViewportSubsection.x, overscannedViewportSubsection.y, overscannedViewportSubsection.width, overscannedViewportSubsection.height);
            parms.SetRow(0, translationAndScale);

            var screenSize = new Vector4(globalScreenSize.x, globalScreenSize.y, 1.0f / globalScreenSize.x, 1.0f / globalScreenSize.y);
            parms.SetRow(1, screenSize);

            var grid = new Vector4(gridSize.x, gridSize.y, 0, 0);
            parms.SetRow(2, grid);

            return parms;
        }

        // there's no *right* way to do it, it simply is a convention
        public static Rect TileIndexToViewportSection(Vector2Int gridSize, int tileIndex)
        {
            if (gridSize.x * gridSize.y == 0)
                return Rect.zero;
            var x = tileIndex % gridSize.x;
            var y = gridSize.y - 1 - tileIndex / gridSize.x; // tile 0 is top-left
            var dx = 1f / (float)gridSize.x;
            var dy = 1f / (float)gridSize.y;
            return new Rect(x * dx, y * dy, dx, dy);
        }

        static Rect Expand(Rect r, Vector2 delta)
        {
            return Rect.MinMaxRect(
                r.min.x - delta.x,
                r.min.y - delta.y,
                r.max.x + delta.x,
                r.max.y + delta.y);
        }

        public static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels)
        {
            return ApplyOverscan(normalizedViewportSubsection, overscanInPixels, Screen.width, Screen.height);
        }

        public static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels, int viewportWidth, int viewportHeight)
        {
            var normalizedOverscan = new Vector2(
                overscanInPixels * (normalizedViewportSubsection.max.x - normalizedViewportSubsection.min.x) / viewportWidth,
                overscanInPixels * (normalizedViewportSubsection.max.y - normalizedViewportSubsection.min.y) / viewportHeight);

            return Expand(normalizedViewportSubsection, normalizedOverscan);
        }

        public static Rect ApplyBezel(Rect normalizedViewportSubsection, Vector2 physicalScreenSizeInMm, Vector2 bezelInMm)
        {
            var normalizedBezel = new Vector2(
                bezelInMm.x / (float)physicalScreenSizeInMm.x,
                bezelInMm.y / (float)physicalScreenSizeInMm.y);

            var bezel = new Vector2(
                normalizedViewportSubsection.width * normalizedBezel.x,
                normalizedViewportSubsection.height * normalizedBezel.y);

            return Rect.MinMaxRect(
                normalizedViewportSubsection.min.x + bezel.x,
                normalizedViewportSubsection.min.y + bezel.y,
                normalizedViewportSubsection.max.x - bezel.x,
                normalizedViewportSubsection.max.y - bezel.y);
        }

        public static Matrix4x4 GetFrustumSlicingAsymmetricProjection(Matrix4x4 originalProjection, Rect normalizedViewportSubsection)
        {
            var baseFrustumPlanes = originalProjection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes();
            frustumPlanes.zNear = baseFrustumPlanes.zNear;
            frustumPlanes.zFar = baseFrustumPlanes.zFar;
            frustumPlanes.left = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMin);
            frustumPlanes.right = Mathf.LerpUnclamped(baseFrustumPlanes.left, baseFrustumPlanes.right, normalizedViewportSubsection.xMax);
            frustumPlanes.bottom = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMin);
            frustumPlanes.top = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top, normalizedViewportSubsection.yMax);
            return Matrix4x4.Frustum(frustumPlanes);
        }
    }
}
