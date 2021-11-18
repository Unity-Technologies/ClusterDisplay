using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and calls Camera.Render() for a single tile.
    /// </summary>
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        private StandardTileRTManager m_RTManager = new StandardTileRTManager();
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardTile;
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        private bool TryPrepareRender (
            Matrix4x4 worldToCameraMatrix,
            Matrix4x4 currentProjectionMatrix, 
            out RenderTexture targetTexture, 
            out Matrix4x4 projectionMatrix, 
            out Matrix4x4 cullingMatrix)
        {
            if (!SetupTiledLayout(
                currentProjectionMatrix,
                out var asymmetricProjectionMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
            {
                targetTexture = null;
                projectionMatrix = currentProjectionMatrix;
                cullingMatrix = currentProjectionMatrix * worldToCameraMatrix;
                return false;
            }

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: k_ClusterRenderer.context.debugSettings.enableKeyword);
            UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

            targetTexture = m_RTManager.GetSourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            projectionMatrix = asymmetricProjectionMatrix;
            cullingMatrix = asymmetricProjectionMatrix * worldToCameraMatrix;
            return true;
        }

        public override void LateUpdate() {}
        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}

        private void BlitToPresent (Camera camera)
        {
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return;

            camera.enabled = !camera.gameObject.activeInHierarchy;

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            Vector4 texBias = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            var presentRT = m_RTManager.GetPresentRT(Screen.width, Screen.height);
            var sourceRT =  m_RTManager.GetSourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            // Whether we are the emitter or cluster display is inactive, we always want to present to the user one frame behind.
            bool presentQueuedFrame = 
                (ClusterDisplay.ClusterDisplayState.IsEmitter || !ClusterDisplay.ClusterDisplayState.IsClusterLogicEnabled) && 
                k_ClusterRenderer.settings.queueEmitterFrames;

            if (presentQueuedFrame)
            {
                var backBufferRT = m_RTManager.GetQueuedFrameRT(Screen.width, Screen.height, k_ClusterRenderer.settings.queueEmitterFrameCount);

                cmd.SetRenderTarget(presentRT);
                Blit(cmd, backBufferRT, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0));

                cmd.SetRenderTarget(backBufferRT);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
                // Thread.Sleep(100);
            }

            else // If this node is a repeater, then DO NOT present one frame behind.
            {
                cmd.SetRenderTarget(presentRT);
                cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
            }

            // k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            UnityEngine.Graphics.Blit(presentRT, null as RenderTexture);

            // WriteRTToFile(presentRT, $"Present.png", overwrite: false);
            // WriteRTToFile(sourceRT, $"Next.png", overwrite: false);
            // Thread.Sleep(100);

            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }

        public override void OnBeforePresent()
        {
            if (!ClusterCameraController.TryGetContextCamera(out var camera))
                return;

            // if (camera.enabled)
            //     return;

            if (!TryPrepareRender(
                camera.worldToCameraMatrix, 
                camera.projectionMatrix, 
                out var targetTexture, 
                out var projectionMatrix, 
                out var cullingMatrix))
                return;

            camera.targetTexture = targetTexture;
            camera.projectionMatrix = projectionMatrix;
            camera.cullingMatrix = cullingMatrix;

            camera.Render();
            BlitToPresent(camera);
        }

        private void WriteRTToFile (RenderTexture rt, string filePath, bool overwrite = true)
        {
            if (System.IO.File.Exists(filePath) && !overwrite)
                return;

            var activeRT = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tempTex = new Texture2D(Screen.width, Screen.height);
            tempTex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            var bytes = tempTex.EncodeToPNG();
            System.IO.File.WriteAllBytes(filePath, bytes);
            RenderTexture.active = activeRT;
        }
    }
}
