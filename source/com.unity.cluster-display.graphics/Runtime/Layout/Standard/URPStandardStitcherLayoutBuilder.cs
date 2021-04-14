#if CLUSTER_DISPLAY_URP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class URPStandardStitcherLayoutBuilder : StandardTileLayoutBuilder
    {
        static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        public URPStandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Blit(CommandBuffer cmd, RTHandle target, Vector4 texBias, Vector4 rtBias)
        {
            s_PropertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), target);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            s_PropertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, m_ClusterRenderer.Settings.Resources.BlitMaterial, 3, MeshTopology.Quads, 4, 1, s_PropertyBlock);

        }
    }
}
#endif
