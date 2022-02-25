using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter
    {
        event Action<PresentArgs> Present;
        Color ClearColor { set; }
        // We expose the output camera since it may be used for capture.
        Camera Camera { get; }
        void SetDelayed(bool value);
        void Enable(GameObject gameObject);
        void Disable();
    }
}
