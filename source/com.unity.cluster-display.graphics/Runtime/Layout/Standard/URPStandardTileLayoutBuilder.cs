#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
    {
    public class URPStandardTileLayoutBuilder : StandardStitcherLayoutBuilder
    {
        static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();
        public URPStandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Blit(CommandBuffer cmd, RTHandle target, Vector4 texBias, Vector4 rtBias)
        {
            s_PropertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), target);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            s_PropertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, m_ClusterRenderer.Settings.Resources.BlitMaterial, 0, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }
    }
}
#endif
