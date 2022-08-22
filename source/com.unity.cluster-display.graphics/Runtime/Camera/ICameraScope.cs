using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface ICameraScope : IDisposable
    {
        void Render(RenderTexture target,
            Matrix4x4? projection,
            Vector4? screenSizeOverride = null,
            Vector4? screenCoordScaleBias = null,
            Vector3? position = null,
            Quaternion? rotation = null);

        void RenderToCubemap(RenderTexture target, Vector3? position = null);
    }
}
