using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter : IDisposable
    {
        void Initialize(GameObject gameObject);
        void SetSource(RenderTexture texture);
    }
}
