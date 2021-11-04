#if CLUSTER_DISPLAY_HDRP
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpStandardTileLayoutBuilder : StandardTileLayoutBuilder
    {
        public HdrpStandardTileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }
    }
}
#endif
