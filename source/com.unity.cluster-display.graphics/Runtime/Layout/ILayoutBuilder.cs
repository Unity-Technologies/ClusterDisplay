using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface ILayoutBuilder : IDisposable, IClusterRendererEventReceiver
    {
        ClusterRenderer.LayoutMode LayoutMode { get; }
        void Update();
    }
}
