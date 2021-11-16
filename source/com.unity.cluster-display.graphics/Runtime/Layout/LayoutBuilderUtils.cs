using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class LayoutBuilderUtils
    {
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        public static void Render(Camera camera, Matrix4x4 projection, Matrix4x4 clusterParams, RenderTexture target)
        {
            camera.targetTexture = target;
            camera.projectionMatrix = projection;
            camera.cullingMatrix = projection * camera.worldToCameraMatrix;
            
            // TODO Make sure this simple way to pass uniforms is conform to HDRP's expectations.
            // We could have to pass this data through the pipeline.
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterParams);

            camera.Render();
            
            // TODO May be wasteful in some instances (stitcher), add optional parameter?
            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }
    }
}
