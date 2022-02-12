using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    internal static class GraphicsUtil
    {
        // Will only be used for Legacy if we end up supporting it.
        // Otherwise see ScreenCoordOverrideUtils in SRP Core.
        const string k_ShaderKeyword = "SCREEN_COORD_OVERRIDE";
        
        // Used to support the camera bridge capture API. 
        static readonly int k_RecorderTempRT = Shader.PropertyToID("TempRecorder");

        static GraphicsFormat s_GraphicsFormat = GraphicsFormat.None;

        public static readonly Vector4 k_IdentityScaleBias = new Vector4(1, 1, 0, 0);

        static class ShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
        }

        public const string k_BlitShaderName = "Hidden/ClusterDisplay/Blit";
        static MaterialPropertyBlock s_PropertyBlock;
        static Material s_BlitMaterial;

        static GraphicsFormat GetGraphicsFormat()
        {
            if (s_GraphicsFormat == GraphicsFormat.None)
            {
#if CLUSTER_DISPLAY_HDRP
                s_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
#else
                s_GraphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
#endif
            }

            return s_GraphicsFormat;
        }
        
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

        public static void Blit(CommandBuffer commandBuffer, in BlitCommand blitCommand, bool flipY)
        {
            Blit(commandBuffer, blitCommand.texture, blitCommand.scaleBiasTex, blitCommand.scaleBiasRT, flipY);
        }
        
        public static void Blit(CommandBuffer cmd, RenderTexture source, Vector4 texBias, Vector4 rtBias, bool flipY)
        {
            var shaderPass = flipY ? 1 : 0;
            var propertyBlock = GetPropertyBlock();

            propertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBias, texBias);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBiasRt, rtBias);
            propertyBlock.SetFloat(ShaderIDs._BlitMipLevel, 0);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), shaderPass, MeshTopology.Quads, 4, 1, propertyBlock);
        }

        public static void AllocateIfNeeded(ref RenderTexture[] rts, int count, int width, int height, string name)
        {
            AllocateIfNeeded(ref rts, count, width, height, GetGraphicsFormat(), name);
        }

        public static void AllocateIfNeeded(ref RenderTexture[] rts, int count, int width, int height, GraphicsFormat format, string name)
        {
            var nameNeedsUpdate = false;

            if (rts == null || count != rts.Length)
            {
                nameNeedsUpdate = true;
                DeallocateIfNeeded(ref rts);
                rts = new RenderTexture[count];
            }

            for (var i = 0; i != count; ++i)
            {
                nameNeedsUpdate |= AllocateIfNeeded(ref rts[i], width, height, format);
            }

            if (nameNeedsUpdate)
            {
                for (var i = 0; i != count; ++i)
                {
                    rts[i].name = $"{name}-{width}X{height}-{i}";
                }
            }
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height)
        {
            return AllocateIfNeeded(ref rt, width, height, GetGraphicsFormat());
        }

        public static bool AllocateIfNeeded(ref RenderTexture rt, int width, int height, GraphicsFormat format)
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

                rt = new RenderTexture(width, height, 1, format, 0);
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
        
#if CLUSTER_DISPLAY_URP || CLUSTER_DISPLAY_HDRP
        /// <summary>
        /// Return true if handle does not match descriptor
        /// </summary>
        /// <param name="handle">RTHandle to check (can be null)</param>
        /// <param name="descriptor">Descriptor for the RTHandle to match</param>
        /// <param name="filterMode">Filtering mode of the RTHandle.</param>
        /// <param name="wrapMode">Addressing mode of the RTHandle.</param>
        /// <param name="isShadowMap">Set to true if the depth buffer should be used as a shadow map.</param>
        /// <param name="anisoLevel">Anisotropic filtering level.</param>
        /// <param name="mipMapBias">Bias applied to mipmaps during filtering.</param>
        /// <param name="name">Name of the RTHandle.</param>
        /// <param name="scaled">Check if the RTHandle has auto scaling enabled if not, check the widths and heights</param>
        /// <returns></returns>
        internal static bool RTHandleNeedsReAlloc(
            RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode,
            bool isShadowMap,
            int anisoLevel,
            float mipMapBias,
            string name,
            bool scaled)
        {
            if (handle == null || handle.rt == null)
                return true;
            if (handle.useScaling != scaled)
                return true;
            if (!scaled && (handle.rt.width != descriptor.width || handle.rt.height != descriptor.height))
                return true;
            return
                handle.rt.descriptor.depthBufferBits != descriptor.depthBufferBits ||
                (handle.rt.descriptor.depthBufferBits == (int)DepthBits.None && !isShadowMap && handle.rt.descriptor.graphicsFormat != descriptor.graphicsFormat) ||
                handle.rt.descriptor.dimension != descriptor.dimension ||
                handle.rt.descriptor.enableRandomWrite != descriptor.enableRandomWrite ||
                handle.rt.descriptor.useMipMap != descriptor.useMipMap ||
                handle.rt.descriptor.autoGenerateMips != descriptor.autoGenerateMips ||
                handle.rt.descriptor.msaaSamples != descriptor.msaaSamples ||
                handle.rt.descriptor.bindMS != descriptor.bindMS ||
                handle.rt.descriptor.useDynamicScale != descriptor.useDynamicScale ||
                handle.rt.descriptor.memoryless != descriptor.memoryless ||
                handle.rt.filterMode != filterMode ||
                handle.rt.wrapMode != wrapMode ||
                handle.rt.anisoLevel != anisoLevel ||
                handle.rt.mipMapBias != mipMapBias ||
                handle.name != name;
        }

        /// <summary>
        /// Re-allocate fixed-size RTHandle if it is not allocated or doesn't match the descriptor
        /// </summary>
        /// <param name="handle">RTHandle to check (can be null)</param>
        /// <param name="descriptor">Descriptor for the RTHandle to match</param>
        /// <param name="filterMode">Filtering mode of the RTHandle.</param>
        /// <param name="wrapMode">Addressing mode of the RTHandle.</param>
        /// <param name="isShadowMap">Set to true if the depth buffer should be used as a shadow map.</param>
        /// <param name="anisoLevel">Anisotropic filtering level.</param>
        /// <param name="mipMapBias">Bias applied to mipmaps during filtering.</param>
        /// <param name="name">Name of the RTHandle.</param>
        /// <returns></returns>
        public static bool ReAllocateIfNeeded(
            ref RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            string name = "")
        {
            if (RTHandleNeedsReAlloc(handle, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name, false))
            {
                handle?.Release();
                handle = RTHandles.Alloc(descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Re-allocate dynamically resized RTHandle if it is not allocated or doesn't match the descriptor
        /// </summary>
        /// <param name="handle">RTHandle to check (can be null)</param>
        /// <param name="scaleFactor">Constant scale for the RTHandle size computation.</param>
        /// <param name="descriptor">Descriptor for the RTHandle to match</param>
        /// <param name="filterMode">Filtering mode of the RTHandle.</param>
        /// <param name="wrapMode">Addressing mode of the RTHandle.</param>
        /// <param name="isShadowMap">Set to true if the depth buffer should be used as a shadow map.</param>
        /// <param name="anisoLevel">Anisotropic filtering level.</param>
        /// <param name="mipMapBias">Bias applied to mipmaps during filtering.</param>
        /// <param name="name">Name of the RTHandle.</param>
        /// <returns>If the RTHandle should be re-allocated</returns>
        public static bool ReAllocateIfNeeded(
            ref RTHandle handle,
            Vector2 scaleFactor,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            string name = "")
        {
            var usingConstantScale = handle != null && handle.useScaling && handle.scaleFactor == scaleFactor;
            if (!usingConstantScale || RTHandleNeedsReAlloc(handle, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name, true))
            {
                handle?.Release();
                handle = RTHandles.Alloc(scaleFactor, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Re-allocate dynamically resized RTHandle if it is not allocated or doesn't match the descriptor
        /// </summary>
        /// <param name="handle">RTHandle to check (can be null)</param>
        /// <param name="scaleFunc">Function used for the RTHandle size computation.</param>
        /// <param name="descriptor">Descriptor for the RTHandle to match</param>
        /// <param name="filterMode">Filtering mode of the RTHandle.</param>
        /// <param name="wrapMode">Addressing mode of the RTHandle.</param>
        /// <param name="isShadowMap">Set to true if the depth buffer should be used as a shadow map.</param>
        /// <param name="anisoLevel">Anisotropic filtering level.</param>
        /// <param name="mipMapBias">Bias applied to mipmaps during filtering.</param>
        /// <param name="name">Name of the RTHandle.</param>
        /// <returns>If an allocation was done</returns>
        public static bool ReAllocateIfNeeded(
            ref RTHandle handle,
            ScaleFunc scaleFunc,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            string name = "")
        {
            var usingScaleFunction = handle != null && handle.useScaling && handle.scaleFactor == Vector2.zero;
            if (!usingScaleFunction || RTHandleNeedsReAlloc(handle, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name, true))
            {
                handle?.Release();
                handle = RTHandles.Alloc(scaleFunc, descriptor, filterMode, wrapMode, isShadowMap, anisoLevel, mipMapBias, name);
                return true;
            }

            return false;
        }
#endif

        public static void SetShaderKeyword(bool enabled)
        {
            SetShaderKeyword(k_ShaderKeyword, enabled);
        }
        
        public static void SetShaderKeyword(string keyword, bool enabled)
        {
            if (Shader.IsKeywordEnabled(keyword) == enabled)
            {
                return;
            }

            if (enabled)
            {
                Shader.EnableKeyword(keyword);
            }
            else
            {
                Shader.DisableKeyword(keyword);
            }
        }

        // Convention, consistent with blit scale-bias for example.
        internal static Vector4 AsScaleBias(Rect rect) => new(rect.width, rect.height, rect.x, rect.y);

        internal static void ExecuteCaptureIfNeeded(Camera camera, CommandBuffer cmd, Color clearColor, Action<PresentArgs> render, bool flipY)
        {
            var captureActions = CameraCaptureBridge.GetCaptureActions(camera);
            if (captureActions != null)
            {
                cmd.GetTemporaryRT(k_RecorderTempRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, GetGraphicsFormat());
                cmd.SetRenderTarget(k_RecorderTempRT);
                cmd.ClearRenderTarget(true, true, clearColor);

                render.Invoke(new PresentArgs
                {
                    CommandBuffer = cmd,
                    FlipY = flipY,
                    CameraPixelRect = camera.pixelRect
                });

                for (captureActions.Reset(); captureActions.MoveNext();)
                {
                    captureActions.Current(k_RecorderTempRT, cmd);
                }
            }
        }
    }
}
