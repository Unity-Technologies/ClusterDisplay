using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
// Borrowed from SRP core Blitter, we want a slightly different API, don't care about older platforms.
// TODO Promote to package?
    static class Blitter_
    {
        static class BlitShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
        }

        const string k_ShaderName = "Hidden/Universal/CoreBlit";

        static readonly Vector4 k_IdentityScaleBias = new Vector4(1, 1, 0, 0);

        static Material s_Material;
        static MaterialPropertyBlock s_PropertyBlock;
        static bool s_Initialized;

        public static void InitializeIfNeeded()
        {
            if (!s_Initialized)
            {
                s_Material = CoreUtils.CreateEngineMaterial(k_ShaderName);
                s_PropertyBlock = new MaterialPropertyBlock();
                s_Initialized = true;
            }
        }

        public static void Dispose()
        {
            CoreUtils.Destroy(s_Material);
            s_Initialized = false;
        }

        public static void BlitQuad(CommandBuffer cmd, RenderTargetIdentifier source)
        {
            BlitQuad(cmd, source, k_IdentityScaleBias, k_IdentityScaleBias, 0, true);
        }

        static void BlitQuad(CommandBuffer cmd, RenderTargetIdentifier source, Vector4 scaleBiasTex, Vector4 scaleBiasRT, int mipLevelTex, bool bilinear)
        {
            cmd.SetGlobalTexture(BlitShaderIDs._BlitTexture, source);
            BlitQuadSourceIsAssigned(cmd, scaleBiasTex, scaleBiasRT, mipLevelTex, bilinear);
        }

        static void BlitQuadSourceIsAssigned(CommandBuffer cmd, Vector4 scaleBiasTex, Vector4 scaleBiasRT, int mipLevelTex, bool bilinear)
        {
            s_PropertyBlock.SetVector(BlitShaderIDs._BlitScaleBias, scaleBiasTex);
            s_PropertyBlock.SetVector(BlitShaderIDs._BlitScaleBiasRt, scaleBiasRT);
            s_PropertyBlock.SetFloat(BlitShaderIDs._BlitMipLevel, mipLevelTex);

            cmd.DrawProcedural(Matrix4x4.identity, s_Material, bilinear ? 3 : 2, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }
    }
}