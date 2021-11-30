using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    readonly struct DefaultCameraScope : ICameraScope
    {
        readonly Camera m_Camera;

        public DefaultCameraScope(Camera camera)
        {
            m_Camera = camera;
        }

        public void Render(Matrix4x4 projection, Vector4 screenSizeOverride, Vector4 screenCoordTransform, RenderTexture target)
        {
            m_Camera.targetTexture = target;
            m_Camera.projectionMatrix = projection;
            m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
            
            // TODO Set Global Shader Uniforms?

            m_Camera.Render();
        }
            
        public void Dispose()
        {
            m_Camera.ResetAspect();
            m_Camera.ResetProjectionMatrix();
            m_Camera.ResetCullingMatrix();
        }
    }
}
