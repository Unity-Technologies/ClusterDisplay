using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class NullPresenter : IPresenter
    {
        static readonly IPresenter s_Instance = new NullPresenter();

        public static IPresenter Instance => s_Instance;

        public event Action<PresentArgs> Present = delegate { };

        NullPresenter() { }

        public void Disable() { }

        public Color ClearColor { get; set; }

        public Camera Camera => null;

        public void Enable()
        {
            throw new InvalidOperationException(
                $"Using {nameof(NullPresenter)}, the current render pipeline is not supported.");
        }
    }
}
