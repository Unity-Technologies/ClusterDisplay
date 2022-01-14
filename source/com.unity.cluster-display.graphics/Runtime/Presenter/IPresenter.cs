using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter
    {
        event Action<CommandBuffer, bool> Present;
        public Color ClearColor { set; }
        // We expose the output camera since it may be used for capture.
        public Camera Camera { get; }
        void Enable(GameObject gameObject);
        void Disable();
    }
}
