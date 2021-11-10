#if CLUSTER_DISPLAY_HDRP
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
        
        HDAdditionalCameraData m_AdditionalCameraData;
        RenderTexture m_RenderTexture;
        
        public void Dispose()
        {
            // We don;t destroy procedural components, we may reuse them
            // or they'll be destroyed with the ClusterRenderer.
            m_AdditionalCameraData.customRender -= OnCustomRender;
            m_RenderTexture = null;
        }

        public void Initialize(GameObject gameObject)
        {
            // Note: we use procedural components.
            // In edge cases, a user could have added a Camera to the GameObject, and we will modify this Camera.
            // The alternative would be to use a hidden procedural GameObject.
            // But it makes lifecycle management more difficult in edit mode as well as debugging.
            // We consider that making components Not Editable is enough to communicate our intent to users.
            m_AdditionalCameraData = ApplicationUtil.GetOrAddComponent<HDAdditionalCameraData>(gameObject);
            
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

        public void SetSource(RenderTexture texture) => m_RenderTexture = texture;

        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera)
        {
            if (m_RenderTexture == null)
            {
                return;
            }
            
            var cmd = CommandBufferPool.Get(k_CommandBufferName);
            cmd.Blit(m_RenderTexture, k_CameraTargetId);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
#endif
