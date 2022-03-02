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

        public static void Blit(CommandBuffer cmd, RenderTexture source, bool flipY)
        {
            Blit(cmd, source, k_IdentityScaleBias, k_IdentityScaleBias, flipY);
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
        
        // Convention, consistent with blit scale-bias for example.
        internal static Vector4 AsScaleBias(Rect rect) => new(rect.width, rect.height, rect.x, rect.y);
    }
}
