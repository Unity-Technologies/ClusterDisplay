using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter
    {
        event Action<PresentArgs> Present;
        public Color ClearColor { set; }
        // We expose the output camera since it may be used for capture.
        void Enable();
        void Disable();
    }
}
