using System;
using UnityEngine;
using UnityEngine.Rendering;

#if CLUSTER_DISPLAY_XR
using UnityEngine.Rendering.HighDefinition;
#endif


namespace Unity.ClusterDisplay.Graphics
{
    public interface ILayoutBuilder
    {
        bool BuildLayout();
    }

    // Interface to be implemented by ClusterRenderer's custom layouts. 
    public interface IXRLayoutBuilder
    {
        bool BuildLayout(XRLayout layout);
    }
}
