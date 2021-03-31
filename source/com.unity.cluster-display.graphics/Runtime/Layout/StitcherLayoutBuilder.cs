using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherLayoutBuilder : LayoutBuilder
    {
        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
    }
}
