#if CLUSTER_DISPLAY_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
    {
    public class HDRPStandardTileLayoutBuilder : StandardStitcherLayoutBuilder
    {
        public HDRPStandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Blit(CommandBuffer cmd, RTHandle target, Vector4 texBias, Vector4 rtBias)
        {
            HDUtils.BlitQuad(cmd, target, texBias, rtBias, 0, true);
        }
    }
}
#endif
