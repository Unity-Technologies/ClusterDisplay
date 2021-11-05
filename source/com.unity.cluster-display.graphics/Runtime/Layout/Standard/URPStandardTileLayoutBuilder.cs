#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpStandardTileLayoutBuilder : StandardTileLayoutBuilder
    {
        public UrpStandardTileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }
    }
}
#endif
