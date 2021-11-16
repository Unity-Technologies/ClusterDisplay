using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class LayoutBuilderUtils
    {
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        public static void UploadClusterDisplayParams(Matrix4x4 clusterDisplayParams)
        {
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterDisplayParams);
        }

        public static void Render(Camera camera, Matrix4x4 projection, Matrix4x4 clusterParams, RenderTexture target)
        {
            camera.targetTexture = target;
            camera.projectionMatrix = projection;
            camera.cullingMatrix = projection * camera.worldToCameraMatrix;
            
            UploadClusterDisplayParams(clusterParams);

            camera.Render();
            
            // TODO May be wasteful in some instances (stitcher), add optional parameter?
            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public static float GetAspect(ClusterRenderContext context, int screenWidth, int screenHeight) => context.GridSize.x * screenWidth / (float)(context.GridSize.y * screenHeight);
    }
}
