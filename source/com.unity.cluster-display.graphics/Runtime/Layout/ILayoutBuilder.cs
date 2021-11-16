using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface ILayoutBuilder : IDisposable
    {
        // TODO mapping implementations to modes does not need to happen within the implementation.
        LayoutMode LayoutMode { get; }
        
        // We pass screen dimensions explicitely since the static Screen API behavior depends on when it is invoked.
        // It may also make testing easier.
        void Render(Camera camera, RenderContext renderContext);
        IEnumerable<BlitCommand> Present(RenderContext renderContext);
    }
}
