using System;
using UnityEngine;

readonly struct CameraScope : IDisposable
{
    readonly Camera m_Camera;
    static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

    public CameraScope(Camera camera)
    {
        m_Camera = camera;
    }

    public void Render(Matrix4x4 projection, Matrix4x4 clusterParams, RenderTexture target)
    {
        m_Camera.targetTexture = target;
        m_Camera.projectionMatrix = projection;
        m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
            
        // TODO Make sure this simple way to pass uniforms is conform to HDRP's expectations.
        // We could have to pass this data through the pipeline.
        Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterParams);

        m_Camera.Render();
    }
    
    public void Render(Matrix4x4 projection, RenderTexture target)
    {
        m_Camera.targetTexture = target;
        m_Camera.projectionMatrix = projection;
        m_Camera.cullingMatrix = projection * m_Camera.worldToCameraMatrix;
            
        m_Camera.Render();
    }
            
    public void Dispose()
    {
        m_Camera.ResetAspect();
        m_Camera.ResetProjectionMatrix();
        m_Camera.ResetCullingMatrix();
    }
}
