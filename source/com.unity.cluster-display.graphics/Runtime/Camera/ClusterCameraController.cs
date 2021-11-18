using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public interface ICameraEventReceiver
    {
        void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
    }

    [System.Serializable]
    public class ClusterCameraController : ClusterRenderer.IClusterRendererEventReceiver
    {
        // Matrix4x4 does not serialize so we need to serialize to Vector4s.

        private PushedProjectionMatrix[] pushedCustomProjectionMatrices;
        private struct PushedProjectionMatrix
        {
            public bool pushed;
            public Matrix4x4 projectionMatrix;
        }

        public bool CustomProjectionMatrixPushed(int targetTileIndex) => 
            pushedCustomProjectionMatrices != null && pushedCustomProjectionMatrices.Length > targetTileIndex ? pushedCustomProjectionMatrices[targetTileIndex].pushed : false;

        public void PushCustomProjectionMatrix (Matrix4x4 customProjectionMatrix, int targetTileIndex)
        {
            if (pushedCustomProjectionMatrices == null || pushedCustomProjectionMatrices.Length - 1 < targetTileIndex)
            {
                if (pushedCustomProjectionMatrices != null && pushedCustomProjectionMatrices.Length > 0)
                {
                    PushedProjectionMatrix[] tempArray = new PushedProjectionMatrix[pushedCustomProjectionMatrices.Length];
                    System.Array.Copy(pushedCustomProjectionMatrices, tempArray, pushedCustomProjectionMatrices.Length);

                    pushedCustomProjectionMatrices = new PushedProjectionMatrix[targetTileIndex + 1];
                    System.Array.Copy(tempArray, pushedCustomProjectionMatrices, tempArray.Length);
                }

                else pushedCustomProjectionMatrices = new PushedProjectionMatrix[targetTileIndex + 1];
            }

            pushedCustomProjectionMatrices[targetTileIndex] = new PushedProjectionMatrix
            {
                pushed = true,
                projectionMatrix = customProjectionMatrix
            };
        }

        public Matrix4x4 PopCustomProjectionMatrix (int targetTileIndex)
        {
            if (pushedCustomProjectionMatrices == null || targetTileIndex > pushedCustomProjectionMatrices.Length - 1)
            {
                ClusterDebug.LogError($"There are no pushed custom projection matrices with the tile index: \"{targetTileIndex}\".");
                return Matrix4x4.identity;
            }

            var pushMatrix = pushedCustomProjectionMatrices[targetTileIndex];
            if (!pushMatrix.pushed)
            {
                ClusterDebug.LogError($"There are no pushed custom projection matrices with the tile index: \"{targetTileIndex}\", verify whether the matrix was ever pushed or consumed before necessary.");
                return Matrix4x4.identity;
            }

            pushMatrix.pushed = false;
            pushedCustomProjectionMatrices[targetTileIndex] = pushMatrix;
            return pushMatrix.projectionMatrix;
        }

        /// <summary>
        /// Current rendering camera.
        /// </summary>
        public static bool TryGetContextCamera (out Camera contextCamera)
        {
            contextCamera = null;
            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry))
                return false;

            if (!cameraContextRegistry.TryGetFocusedCameraContextTarget(out var focusedCameraContextTarget))
                return false;

            if (!focusedCameraContextTarget.TryGetCamera(out var camera))
            {
                cameraContextRegistry.UnRegister(focusedCameraContextTarget, destroy: true);
                return false;
            }

            return (contextCamera = camera) != null;
        }

        public static bool TryGetPreviousCameraContext (out Camera previousCameraContext)
        {
            previousCameraContext = null;
            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry))
                return false;

            if (!cameraContextRegistry.TryGetPreviousFocusedCameraContextTarget(out var previousFocusedCameraContextTarget))
                return false;

            if (!previousFocusedCameraContextTarget.TryGetCamera(out var camera))
            {
                cameraContextRegistry.UnRegister(previousFocusedCameraContextTarget, destroy: true);
                return false;
            }

            return (previousCameraContext = camera) != null;
        }


        public delegate void OnCameraContextChange(Camera previousCamera, Camera nextCamera);
        private OnCameraContextChange onCameraChange;

        // private Presenter m_Presenter;
        // public Presenter presenter
        // {
        //     get => m_Presenter;
        //     set
        //     {
        //         if (m_Presenter != null)
        //         {
        //             UnRegisterCameraEventReceiver(m_Presenter);
        //             m_Presenter.Dispose();
        //         }

        //         m_Presenter = value;
        //         if (m_Presenter != null)
        //             RegisterCameraEventReceiver(m_Presenter);
        //     }
        // }

        public bool CameraIsInContext(Camera camera) => TryGetContextCamera(out var contextCamera) && contextCamera == camera;

        public void RegisterCameraEventReceiver (ICameraEventReceiver cameraEventReceiver) => onCameraChange += cameraEventReceiver.OnCameraContextChange;
        public void UnRegisterCameraEventReceiver (ICameraEventReceiver cameraEventReceiver) => onCameraChange -= cameraEventReceiver.OnCameraContextChange;

        protected virtual void OnPollFrameSettings (Camera camera) {}

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}

        public void OnBeginCameraRender (ScriptableRenderContext context, Camera camera)
        {
            if (!CameraContextRegistry.CanChangeContextTo(camera))
                return;

            // If we are beginning to render with our context camera, do nothing.
            if (CameraIsInContext(camera))
            {
                // m_Presenter.PollCamera(camera);
                return;
            }

            if (!CameraContextRegistry.TryGetInstance(out var cameraContextRegistry) ||
                !cameraContextRegistry.TryGetCameraContextTarget(camera, out var nextCameraContext))
            {
                // m_Presenter.PollCamera(camera);
                return;
            }

            cameraContextRegistry.SetFocusedCameraContextTarget(nextCameraContext);
            OnPollFrameSettings(camera);

            TryGetPreviousCameraContext(out var previousCameraContext);
            if (onCameraChange != null)
                onCameraChange(previousCameraContext, camera);

            // m_Presenter.PollCamera(camera);
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}
    }
}
