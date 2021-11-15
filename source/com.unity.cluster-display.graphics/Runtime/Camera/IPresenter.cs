using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface IPresenter
    {
        void Enable(GameObject gameObject);
        void Disable();
        void SetSource(RenderTexture texture);
    }
}
