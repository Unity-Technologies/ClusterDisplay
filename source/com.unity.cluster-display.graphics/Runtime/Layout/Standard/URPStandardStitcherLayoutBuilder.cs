#if CLUSTER_DISPLAY_URP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class URPStandardStitcherLayoutBuilder : StandardStitcherLayoutBuilder
    {
        static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        public URPStandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
    }
}
#endif
