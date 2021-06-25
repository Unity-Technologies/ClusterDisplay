#if CLUSTER_DISPLAY_HDRP && CLUSTER_DISPLAY_XR
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class XRStitcherLayoutBuilder : StitcherLayoutBuilder, IXRLayoutBuilder
    {
        private bool m_HasClearedMirrorView = true;
        private Rect m_OverscannedRect;

        private XRStitcherRTManager m_RTManager = new XRStitcherRTManager();
        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.XRStitcher;
        public XRStitcherLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
            m_HasClearedMirrorView = true;
        }
        
        public override void Dispose()
        {
            m_QueuedStitcherParameters.Clear();
            m_RTManager.Release();
        }

        public override void LateUpdate()
        {
            if (k_ClusterRenderer.cameraController.TryGetContextCamera(out var contextCamera))
                contextCamera.enabled = true;
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            Assert.IsFalse(m_QueuedStitcherParameters.Count == 0);
            var parms = m_QueuedStitcherParameters.Dequeue();

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(k_ClusterRenderer.context.gridSize, parms.tileIndex);

            croppedViewport.x *= croppedSize.x;
            croppedViewport.y *= croppedSize.y;
            croppedViewport.width *= croppedSize.x;
            croppedViewport.height *= croppedSize.y;

            cmd.SetRenderTarget(rt);
            cmd.SetViewport(croppedViewport);
            
            if (!m_HasClearedMirrorView)
            {
                m_HasClearedMirrorView = true;
                cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
            }

            var sourceRT = parms.sourceRT as RTHandle;
            if (sourceRT == null)
            {
                Debug.LogError($"Invalid {nameof(RTHandle)}");
                return;
            }

            HDUtils.BlitQuad(cmd, sourceRT, parms.scaleBiasTex, parms.scaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            if (!ValidGridSize(out var numTiles))
                return false;

            var camera = layout.camera;
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return false;

            if (!camera.enabled)
                camera.enabled = true;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return false;

            // Whenever we build a new layout we expect previously submitted mirror params to have been consumed.
            Assert.IsTrue(m_QueuedStitcherParameters.Count == 0);
            m_HasClearedMirrorView = false;

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = camera.projectionMatrix;
            
            for (var tileIndex = 0; tileIndex != numTiles; ++tileIndex)
            {
                var sourceRT = m_RTManager.GetSourceRT(numTiles, tileIndex, (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

                CalculateStitcherLayout(
                    camera, 
                    cachedProjectionMatrix,
                    tileIndex, 
                    out var percentageViewportSubsection, 
                    out var viewportSubsection, 
                    out var asymmetricProjectionMatrix);

                cullingParams.stereoProjectionMatrix = asymmetricProjectionMatrix;
                cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
                cullingParams.cullingMatrix = camera.worldToCameraMatrix * asymmetricProjectionMatrix ;
               
                CalculcateAndQueueStitcherParameters(tileIndex, sourceRT, m_OverscannedRect, percentageViewportSubsection);

                var clusterDisplayParams = GraphicsUtil.GetClusterDisplayParams(
                        viewportSubsection,
                        k_ClusterRenderer.context.globalScreenSize,
                        k_ClusterRenderer.context.gridSize);

                var passInfo = new XRPassCreateInfo
                {
                    multipassId = tileIndex,
                    cullingPassId = tileIndex,
                    cullingParameters = cullingParams,
                    renderTarget = sourceRT,
                    customMirrorView = BuildMirrorView
                };

                var viewInfo = new XRViewCreateInfo
                {
                    viewMatrix = camera.worldToCameraMatrix,
                    projMatrix = asymmetricProjectionMatrix,
                    viewport = m_OverscannedRect,
<<<<<<< HEAD
                    clusterDisplayParams = GraphicsUtil.GetClusterDisplayParams(
                        viewportSubsection, 
                        m_ClusterRenderer.Context.GlobalScreenSize, 
                        m_ClusterRenderer.Context.GridSize),
=======
                    clusterDisplayParams = clusterDisplayParams,
>>>>>>> 2d66b570e0aed08c93a752d6ed14377986100698
                    textureArraySlice = -1
                };

                XRPass pass = layout.CreatePass(passInfo);
                layout.AddViewToPass(viewInfo, pass);
            }

            return true;
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
<<<<<<< HEAD
            if (!m_ClusterRenderer.CameraController.CameraIsInContext(camera))
=======
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var contextCamera) || camera != contextCamera)
>>>>>>> 2d66b570e0aed08c93a752d6ed14377986100698
                return;

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: m_ClusterRenderer.Context.DebugSettings.EnableKeyword);
            camera.targetTexture = null;
        }

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) 
        {
            if (!m_ClusterRenderer.CameraController.CameraIsInContext(camera))
                return;
            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: false);
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
#endif
