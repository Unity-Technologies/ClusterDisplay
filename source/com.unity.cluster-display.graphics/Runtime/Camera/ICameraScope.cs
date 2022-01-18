using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface ICameraScope : IDisposable
    {
        void Render(int nodeId, Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordScaleBias, RenderTexture target);
        void Render(int nodeId, Matrix4x4 projection, RenderTexture target);
    }
}
