#if CLUSTER_DISPLAY_HDRP
using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpPresenter : SrpPresenter, IPresenter
    {
        public event Action<PresentArgs> Present = delegate {};

        bool m_Delayed;
        HDAdditionalCameraData m_AdditionalCameraData;

        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;
        
        protected override Action<PresentArgs> GetPresentAction() => Present;

        public override void Disable()
        {
            // We don't destroy procedural components, we may reuse them
            // or they'll be destroyed with the ClusterRenderer.
            if (m_Delayed)
            {
                m_AdditionalCameraData.customRender -= OnCustomRenderDelayed;
            }
            else
            {
                m_AdditionalCameraData.customRender -= OnCustomRender;
            }
            
            base.Disable();
        }

        public void Enable(GameObject gameObject, bool delayByOneFrame)
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

            m_Delayed = delayByOneFrame;
            
            if (m_Delayed)
            {
                m_AdditionalCameraData.customRender += OnCustomRenderDelayed;
            }
            else
            {
                m_AdditionalCameraData.customRender += OnCustomRender;
            }
        }

        RTHandle GetBackBuffer() => m_AdditionalCameraData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color);
        
        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera) => DoPresent(context, GetBackBuffer());
        
        void OnCustomRenderDelayed(ScriptableRenderContext context, HDCamera hdCamera) => DoPresentDelayed(context, GetBackBuffer());
    }
}
#endif
