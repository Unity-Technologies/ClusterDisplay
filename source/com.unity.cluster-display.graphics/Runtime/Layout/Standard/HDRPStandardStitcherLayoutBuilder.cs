#if CLUSTER_DISPLAY_HDRP
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpStandardStitcherLayoutBuilder : StandardStitcherLayoutBuilder
    {
        public HdrpStandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }
    }
}
#endif
