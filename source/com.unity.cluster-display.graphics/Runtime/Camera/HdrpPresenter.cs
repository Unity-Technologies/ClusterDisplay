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
        
        public event Action<CommandBuffer> Present = delegate {};

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
            m_AdditionalCameraData.customRender -= OnCustomRender;
        }

        public void Enable(GameObject gameObject)
        {
            // Note: we use procedural components.
            // In edge cases, a user could have added a Camera to the GameObject, and we will modify this Camera.
            // The alternative would be to use a hidden procedural GameObject.
            // But it makes lifecycle management more difficult in edit mode as well as debugging.
            // We consider that making components Not Editable is enough to communicate our intent to users.
            m_AdditionalCameraData = gameObject.GetOrAddComponent<HDAdditionalCameraData>();
            
            // HDAdditionalCameraData requires a Camera so no need to add it manually.
            var camera = gameObject.GetComponent<Camera>();
            Assert.IsNotNull(camera);
            // We use the camera to blit to screen.
            camera.targetTexture = null;
            camera.hideFlags = HideFlags.NotEditable;
            
            // Assigning a customRender will bypass regular camera rendering,
            // so we don't need to worry about the camera render involving wasteful operations.
            m_AdditionalCameraData.hideFlags = HideFlags.NotEditable;
            m_AdditionalCameraData.customRender += OnCustomRender;
        }

        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
        {
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            
            // TODO is this needed?
            cmd.SetRenderTarget(k_CameraTargetId);
            cmd.ClearRenderTarget(true, true, m_ClearColor);

            Present.Invoke(cmd);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
