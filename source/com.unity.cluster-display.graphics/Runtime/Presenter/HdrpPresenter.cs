#if CLUSTER_DISPLAY_HDRP
using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpPresenter : IPresenter
    {
        const string k_CommandBufferName = "Present To Screen";
        readonly RenderTargetIdentifier k_CameraTargetId = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        
        public event Action<PresentArgs> Present = delegate {};

        Camera m_Camera;
        HDAdditionalCameraData m_AdditionalCameraData;
        Color m_ClearColor;

        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;
        
        public void Disable()
        {
            // We don't destroy procedural components, we may reuse them
            // or they'll be destroyed with the ClusterRenderer.
            m_AdditionalCameraData.customRender -= OnCustomRender;
        }

        public void Enable(GameObject gameObject)
        {
            // Note: we use procedural components.
            // In edge cases, a user could have added a Camera to the GameObject, and we will modify this Camera.
            // The alternative would be to use a hidden procedural GameObject.
            // But it makes lifecycle management more difficult in edit mode as well as debugging.
            // We consider that making components Not Editable is enough to communicate our intent to users.
            m_Camera = gameObject.GetOrAddComponent<Camera>();
            // We use the camera to blit to screen.
            m_Camera.targetTexture = null;
            m_Camera.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
            
            m_AdditionalCameraData = gameObject.GetOrAddComponent<HDAdditionalCameraData>();
            m_AdditionalCameraData.flipYMode = HDAdditionalCameraData.FlipYMode.ForceFlipY;

            // Assigning a customRender will bypass regular camera rendering,
            // so we don't need to worry about the camera render involving wasteful operations.
            m_AdditionalCameraData.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
            m_AdditionalCameraData.customRender += OnCustomRender;
        }

        RenderTexture m_LastFrame;
        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
        {
			if (ClusterDisplayState.IsEmitter && ClusterDisplayState.EmitterIsHeadless)
                return;
			
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            
            GraphicsUtil.ExecuteCaptureIfNeeded(m_Camera, cmd, m_ClearColor, Present.Invoke, false);
			var handle = m_AdditionalCameraData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color);

            if (Application.isPlaying && 
                ClusterDisplayState.IsEmitter &&
                CommandLineParser.delayRepeaters)
            {
                ClusterDebug.Log($"Emitter presenting previous frame: {ClusterDisplayState.Frame - 1}");

                if (m_LastFrame == null ||
                    m_LastFrame.width != m_Camera.pixelWidth ||
                    m_LastFrame.height != m_Camera.pixelHeight ||
                    m_LastFrame.depth != handle.rt.depth ||
                    m_LastFrame.graphicsFormat != handle.rt.graphicsFormat)
                {
                    if (m_LastFrame != null)
                    {
                        m_LastFrame.DiscardContents();
                        m_LastFrame = null;
                    }

                    m_LastFrame = new RenderTexture(
                        m_Camera.pixelWidth,
                        m_Camera.pixelHeight,
                        handle.rt.depth,
                        handle.rt.graphicsFormat,
                        0);
                    m_LastFrame.antiAliasing = 1;
                    m_LastFrame.wrapMode = TextureWrapMode.Repeat;
                    m_LastFrame.filterMode = FilterMode.Point;

                    ClusterDebug.Log($"Created new buffer for storing previous frame:\n\tWidth: {hdCamera.actualWidth}\n\tHeight: {hdCamera.actualHeight}\n\tDepth: {handle.rt.depth}\n\tGraphics Format: {handle.rt.graphicsFormat}");
                }

                cmd.SetRenderTarget(k_CameraTargetId);
                cmd.ClearRenderTarget(true, true, m_ClearColor);

                cmd.Blit(m_LastFrame, k_CameraTargetId, new Vector2(1, 1), Vector2.zero);
                cmd.SetRenderTarget(m_LastFrame);
            }
            else
            {
                ClusterDebug.Log($"Repeater presenting current frame: {ClusterDisplayState.Frame}");
                cmd.SetRenderTarget(k_CameraTargetId);
            }

            cmd.ClearRenderTarget(true, true, m_ClearColor);

            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = !(ClusterDisplayState.IsEmitter && CommandLineParser.delayRepeaters),
                CameraPixelRect = m_Camera.pixelRect
            });
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
