using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface ICameraScope : IDisposable
    {
        void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordTransform, RenderTexture target);
        //void Dispose();
    }
}
