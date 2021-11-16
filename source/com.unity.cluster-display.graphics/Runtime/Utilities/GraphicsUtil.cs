using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    static class GraphicsUtil
    {
        static class ShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
        }

        const string k_ShaderKeyword = "USING_CLUSTER_DISPLAY";
        const string k_BlitShaderName = "ClusterDisplay/Blit";
        static readonly Vector4 k_IdentityScaleBiasRT = new Vector4(1, 1, 0, 0);
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

                // TODO we had a utility adding shader to the included list, bring it on.
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

        // TODO Remove?
        public static void Blit(CommandBuffer cmd, RenderTexture source, Vector4 texBias, bool flipY)
        {
            Blit(cmd, source, texBias, k_IdentityScaleBiasRT, flipY);
        }

        public static void Blit(CommandBuffer cmd, RenderTexture source, Vector4 texBias, Vector4 rtBias, bool flipY)
        {
            var shaderPass = flipY ? 1 : 0;
            var propertyBlock = GetPropertyBlock();

            // TODO Use static ints...
            propertyBlock.SetTexture(ShaderIDs._BlitTexture, source);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBias, texBias);
            propertyBlock.SetVector(ShaderIDs._BlitScaleBiasRt, rtBias);
            propertyBlock.SetFloat(ShaderIDs._BlitMipLevel, 0);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), shaderPass, MeshTopology.Quads, 4, 1, propertyBlock);
        }

        public static void AllocateIfNeeded(ref RenderTexture[] rts, int count, string name, int width, int height, GraphicsFormat format)
        {
            if (rts == null || count != rts.Length)
            {
                DeallocateIfNeeded(ref rts);
                rts = new RenderTexture[count];
            }

            // TODO name rarely used, inefficient
            // TODO we populate these arrays all at once,
            // we can assume all tex are similar, just check the 1st
            for (var i = 0; i != count; ++i)
            {
                AllocateIfNeeded(ref rts[i], $"{name}-{i}", width, height, format);
            }
        }

        public static void AllocateIfNeeded(ref RenderTexture rt, string name, int width, int height, GraphicsFormat format)
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
            }
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

        public static void SetShaderKeyword(bool enabled)
        {
            if (Shader.IsKeywordEnabled(k_ShaderKeyword) == enabled)
            {
                return;
            }

            if (enabled)
            {
                Shader.EnableKeyword(k_ShaderKeyword);
            }
            else
            {
                Shader.DisableKeyword(k_ShaderKeyword);
            }
        }
    }
}
