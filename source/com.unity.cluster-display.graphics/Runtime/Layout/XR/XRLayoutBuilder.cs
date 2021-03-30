using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
#if CLUSTER_DISPLAY_XR
    public abstract class XRLayoutBuilder : LayoutBuilder
    {
        protected XRLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
    }
#endif
}
