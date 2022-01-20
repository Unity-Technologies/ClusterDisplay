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

        HDAdditionalCameraData m_AdditionalCameraData;
        Color m_ClearColor;

        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public void Disable()
        {
            // We don't destroy procedural components, we may reuse them
            // or they'll be destroyed with the ClusterRenderer.
            if (m_AdditionalCameraData != null)
                m_AdditionalCameraData.customRender -= OnCustomRender;
        }

        public void Enable()
        {
            if (PresenterCamera.Camera == null)
                return;
            
            // HDAdditionalCameraData requires a Camera so no need to add it manually.
            m_AdditionalCameraData = PresenterCamera.Camera.gameObject.GetOrAddComponent<HDAdditionalCameraData>();
            
            // Note: we use procedural components.
            // In edge cases, a user could have added a Camera to the GameObject, and we will modify this Camera.
            // The alternative would be to use a hidden procedural GameObject.
            // But it makes lifecycle management more difficult in edit mode as well as debugging.
            // We consider that making components Not Editable is enough to communicate our intent to users.
            m_AdditionalCameraData.flipYMode = HDAdditionalCameraData.FlipYMode.ForceFlipY;
            
            // We use the camera to blit to screen.
            PresenterCamera.Camera.targetTexture = null;
            PresenterCamera.Camera.hideFlags = HideFlags.HideAndDontSave;
            
            // Assigning a customRender will bypass regular camera rendering,
            // so we don't need to worry about the camera render involving wasteful operations.
            m_AdditionalCameraData.hideFlags = HideFlags.DontSave;
            
            m_AdditionalCameraData.customRender -= OnCustomRender;
            m_AdditionalCameraData.customRender += OnCustomRender;
        }

        RenderTexture m_LastFrame;
        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            
            GraphicsUtil.ExecuteCaptureIfNeeded(PresenterCamera.Camera, cmd, m_ClearColor, Present.Invoke, false);
            var handle = m_AdditionalCameraData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color);

            if (ClusterDisplayState.IsEmitter && Application.isPlaying)
            {
                ClusterDebug.Log($"Emitter presenting previous frame: {ClusterDisplayState.Frame - 1}");

                if (m_LastFrame == null ||
                    m_LastFrame.width != hdCamera.actualWidth ||
                    m_LastFrame.height != hdCamera.actualHeight ||
                    m_LastFrame.depth != handle.rt.depth ||
                    m_LastFrame.graphicsFormat != handle.rt.graphicsFormat)
                {
                    if (m_LastFrame != null)
                    {
                        m_LastFrame.DiscardContents();
                        m_LastFrame = null;
                    }

                    m_LastFrame = new RenderTexture(
                        hdCamera.actualWidth,
                        hdCamera.actualHeight,
                        handle.rt.depth,
                        handle.rt.graphicsFormat,
                        0);
                    m_LastFrame.antiAliasing = 1;
                    m_LastFrame.wrapMode = TextureWrapMode.Repeat;
                    m_LastFrame.filterMode = FilterMode.Point;

                    ClusterDebug.Log($"Created new buffer for storing previous frame.");
                }

                cmd.SetRenderTarget(k_CameraTargetId);
                cmd.ClearRenderTarget(true, true, m_ClearColor);

                cmd.Blit(m_LastFrame, k_CameraTargetId, new Vector2(1, -1), Vector2.zero);
                cmd.SetRenderTarget(m_LastFrame);
            }
            else
            {
                cmd.SetRenderTarget(k_CameraTargetId);
                cmd.ClearRenderTarget(true, true, m_ClearColor);
            }

            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = true,
                CameraPixelRect = PresenterCamera.Camera.pixelRect
            });
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
