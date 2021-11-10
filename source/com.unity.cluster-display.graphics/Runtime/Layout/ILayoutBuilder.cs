using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface ILayoutBuilder : IDisposable
    {
        // TODO mapping implementations to modes does not need to happen within the implementation.
        LayoutMode LayoutMode { get; }
        RenderTexture PresentRT { get; }
        void Render(Camera camera);
        void Present();
    }
}
