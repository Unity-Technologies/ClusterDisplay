using System;
using UnityEngine;
using UnityEngine.Rendering;
#if CLUSTER_DISPLAY_XR
using UnityEngine.Rendering.HighDefinition;

#endif

namespace Unity.ClusterDisplay.Graphics
{
    // TODO Empty interface is often a code smell?
    interface ILayoutBuilder { }

    // Interface to be implemented by ClusterRenderer's custom layouts. 
#if CLUSTER_DISPLAY_XR
    interface IXRLayoutBuilder
    {
        bool BuildLayout(XRLayout layout);
        void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport);
    }
#endif
}
