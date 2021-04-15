using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class XRPresenter : Presenter
    {
        public override RenderTexture PresentRT { get; set; }

        public override void Dispose() {}
        protected override void DeinitializeCamera(Camera camera) {}
        protected override void InitializeCamera(Camera camera) {}
    }
}
