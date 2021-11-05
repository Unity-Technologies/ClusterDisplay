#if CLUSTER_DISPLAY_URP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class UrpStandardStitcherLayoutBuilder : StandardStitcherLayoutBuilder
    {
        public UrpStandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
    }
}
#endif
