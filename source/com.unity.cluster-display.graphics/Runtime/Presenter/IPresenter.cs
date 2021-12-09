using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter
    {
        event Action<CommandBuffer> Present;
        public Color ClearColor { set; }
        void Enable(GameObject gameObject);
        void Disable();
    }
}
