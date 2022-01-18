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

        public Camera Camera => PresenterCamera.Camera;
        
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

        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            
            GraphicsUtil.ExecuteCaptureIfNeeded(Camera, cmd, m_ClearColor, Present.Invoke, false);

            cmd.SetRenderTarget(k_CameraTargetId);
            cmd.ClearRenderTarget(true, true, m_ClearColor);

            Present.Invoke(new PresentArgs
            {
                CommandBuffer = cmd,
                FlipY = true,
                CameraPixelRect = Camera.pixelRect
            });
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
