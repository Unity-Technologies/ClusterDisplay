#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
    {
    public class URPStandardTileLayoutBuilder : StandardTileLayoutBuilder
    {
        public URPStandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
    }
}
#endif
