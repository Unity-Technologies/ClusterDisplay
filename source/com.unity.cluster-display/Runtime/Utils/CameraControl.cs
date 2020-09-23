using System;
using Unity.ClusterDisplay;
using UnityEngine;

namespace ClusterDisplay.Utils
{
    //[ExecuteInEditMode]
    [Serializable]
    public class CameraControl : MonoBehaviour
    {
        public  int numTilesX = 2;
        public  int numTilesY = 2;

        private Matrix4x4 cameraBackup;
        private bool restoreCam = false;

        void Update()
        {
            if (restoreCam)
                Camera.main.projectionMatrix = cameraBackup;
            restoreCam = false;
        }

        void LateUpdate()
        {
            if (!ClusterSync.Active)
            {
                return;
            }

            var localTile = ClusterSync.Active ? ClusterSync.Instance.DynamicLocalNodeId : 0;
            if (localTile > numTilesX * numTilesY - 1)
                return;
   

            var fx = localTile % numTilesX;
            var fy = localTile / numTilesX;

            if (Camera.main == null)
            {
                return;
            }

            restoreCam = true;

            cameraBackup = Camera.main.projectionMatrix;
            var frustumPlanes = Camera.main.projectionMatrix.decomposeProjection;
            var frustum = frustumPlanes;
            frustum.left = Mathf.Lerp(frustumPlanes.left, frustumPlanes.right, (float)fx / numTilesX);
            frustum.right = Mathf.Lerp(frustumPlanes.left, frustumPlanes.right, ((float)fx+1) / numTilesX);
            frustum.top = Mathf.Lerp(frustumPlanes.top, frustumPlanes.bottom, (float)fy / numTilesY);
            frustum.bottom = Mathf.Lerp(frustumPlanes.top, frustumPlanes.bottom, ((float)fy+1) / numTilesY);
            Camera.main.projectionMatrix = Matrix4x4.Frustum(frustum);
        }
    }
}