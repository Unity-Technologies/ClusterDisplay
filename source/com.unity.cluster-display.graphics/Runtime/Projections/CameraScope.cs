using System;
using Unity.ClusterDisplay;
using UnityEngine;
using Unity.ClusterDisplay.Graphics;

namespace Unity.ClusterDisplay
{
    readonly struct CameraScope : IDisposable
    {
        private readonly ClusterRenderer.PreRenderCameraDataOverride m_preRenderCameraDataOverride;
        
        readonly Camera m_Camera;
        readonly int m_cullingMask;
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        public CameraScope(
            ClusterRenderer.PreRenderCameraDataOverride preRenderCameraDataOverride, 
            Camera camera)
        {
            m_preRenderCameraDataOverride = preRenderCameraDataOverride;
            m_Camera = camera;
            m_cullingMask = camera.cullingMask;
        }

        public void Render(
            int nodeId,
            Matrix4x4 projection, 
            Matrix4x4 clusterParams, 
            RenderTexture target)
        {
            // TODO Make sure this simple way to pass uniforms is conform to HDRP's expectations.
            // We could have to pass this data through the pipeline.
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterParams);

            Render(nodeId, projection, target);
        }
        
        public void Render(int nodeId, Matrix4x4 projectionMatrix, RenderTexture target)
        {
            // Do not render objects that are part of the cluster rendering infrastructure, e.g. projection surfaces
            var mask = m_Camera.cullingMask;
            mask &= ~(1 << ClusterRenderer.VirtualObjectLayer);
            
            m_Camera.cullingMask = mask;
            m_Camera.targetTexture = target;

            var oldProjectionMatrix = m_Camera.projectionMatrix;
            var position = m_Camera.transform.position;
            var oldPosition = position;
            
            var rotation = m_Camera.transform.rotation;
            var oldRotation = rotation;
            
            m_preRenderCameraDataOverride?.Invoke(nodeId, ref position, ref rotation, ref projectionMatrix);
            
            m_Camera.transform.position = position;
            m_Camera.transform.rotation = rotation;
            
            m_Camera.projectionMatrix = projectionMatrix;
            m_Camera.cullingMatrix = projectionMatrix * m_Camera.worldToCameraMatrix;
            
            ClusterDebug.Log($"Calling render on camera: \"{m_Camera.gameObject.name}\".");
            m_Camera.Render();
            
            m_Camera.transform.position = oldPosition;
            m_Camera.transform.rotation = oldRotation;

            m_Camera.projectionMatrix = oldProjectionMatrix;
            m_Camera.cullingMatrix = oldProjectionMatrix * m_Camera.worldToCameraMatrix;
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