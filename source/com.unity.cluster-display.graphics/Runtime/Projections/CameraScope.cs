using System;
using Unity.ClusterDisplay;
using UnityEngine;
using Unity.ClusterDisplay.Graphics;

namespace Unity.ClusterDisplay
{
    readonly struct CameraScope : IDisposable
    {
        readonly Camera m_Camera;
        readonly int m_cullingMask;
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        public CameraScope(Camera camera)
        {
            m_Camera = camera;
            m_cullingMask = camera.cullingMask;
        }

        public void Render(Matrix4x4 projection, Matrix4x4 clusterParams, RenderTexture target)
        {
            // TODO Make sure this simple way to pass uniforms is conform to HDRP's expectations.
            // We could have to pass this data through the pipeline.
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterParams);

            Render(projection, target);
        }

        public void Render(Matrix4x4 projection, RenderTexture target)
        {
            // Do not render objects that are part of the cluster rendering infrastructure, e.g. projection surfaces
            var mask = m_Camera.cullingMask;
            mask &= ~(1 << ClusterRenderer.VirtualObjectLayer);
            m_Camera.cullingMask = mask;

            m_Camera.targetTexture = target;
            m_Camera.projectionMatrix = projection;
            m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;

            ClusterDebug.Log($"Calling render on camera: \"{m_Camera.gameObject.name}\".");
            m_Camera.Render();
        }

        public void Dispose()
        {
            m_Camera.ResetAspect();
            m_Camera.ResetProjectionMatrix();
            m_Camera.ResetCullingMatrix();
            m_Camera.cullingMask = m_cullingMask;
        }
    }
}