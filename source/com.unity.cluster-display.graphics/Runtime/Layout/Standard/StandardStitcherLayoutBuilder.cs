using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardStitcherLayoutBuilder : StitcherLayoutBuilder, ILayoutBuilder
    {
        public StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        public bool BuildLayout()
        {
            return false;
        }

        public void BuildMirrorView()
        {
        }

        public override void Dispose()
        {
        }
    }
}
