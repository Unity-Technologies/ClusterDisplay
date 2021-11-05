using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO remove this class.
    /// <summary>
    /// In XR mode, the presenter doesn't really do anything.
    /// </summary>
    class XRPresenter : Presenter
    {
        public override RenderTexture PresentRT
        {
            set { }
        }

        public override void Dispose() { }
        protected override void DeinitializeCamera(Camera camera) { }
        protected override void InitializeCamera(Camera camera) { }
    }
}
