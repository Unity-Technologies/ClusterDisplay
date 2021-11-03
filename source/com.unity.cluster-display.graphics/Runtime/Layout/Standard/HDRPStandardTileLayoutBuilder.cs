#if CLUSTER_DISPLAY_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class HDRPStandardTileLayoutBuilder : StandardTileLayoutBuilder
    {
        public HDRPStandardTileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }
    }
}
#endif
