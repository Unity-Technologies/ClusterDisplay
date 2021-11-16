using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class NullPresenter : IPresenter
    {
        public event Action<CommandBuffer> Present = delegate {};

        public void Disable() { }

        public Color ClearColor { get; set; }

        public void Enable(GameObject gameObject)
        {
            // TODO Throw an error?
            Debug.LogError($"Using {nameof(NullPresenter)}, the current render pipeline is not supported.");
        }
    }
}
