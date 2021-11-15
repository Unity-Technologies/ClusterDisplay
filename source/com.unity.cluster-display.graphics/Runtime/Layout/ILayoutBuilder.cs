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
        
        // We pass screen dimensions explicitely since the static Screen API behavior depends on when it is invoked.
        // It may also make testing easier.
        void Render(Camera camera, int screenWidth, int screenHeight);
        void Present(CommandBuffer commandBuffer, int screenWidth, int screenHeight);
    }
}
