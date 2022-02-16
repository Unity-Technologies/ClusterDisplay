#if CLUSTER_DISPLAY_HDRP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class HdrpPresenter : SrpPresenter, IPresenter
    {
        static readonly RTHandle k_CameraTarget = RTHandles.Alloc(BuiltinRenderTextureType.CameraTarget);
        
        HDAdditionalCameraData m_AdditionalCameraData;

        public Color ClearColor
        {
            set => m_ClearColor = value;
        }

        public Camera Camera => m_Camera;
        
        public override void Enable(GameObject gameObject)
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

            base.Enable(gameObject);
        }

        protected override void Bind(bool delayed)
        {
            if (delayed)
            {
                m_AdditionalCameraData.customRender += OnCustomRenderDelayed;
            }
            else
            {
                m_AdditionalCameraData.customRender += OnCustomRender;
            }
        }

        protected override void Unbind(bool delayed)
        {
            if (delayed)
            {
                m_AdditionalCameraData.customRender -= OnCustomRenderDelayed;
            }
            else
            {
                m_AdditionalCameraData.customRender -= OnCustomRender;
            }
        }

        void OnCustomRender(ScriptableRenderContext context, HDCamera hdCamera) => DoPresent(context, k_CameraTarget, true);

        void OnCustomRenderDelayed(ScriptableRenderContext context, HDCamera hdCamera)
        {
            var intermediateTargetDescriptor = m_AdditionalCameraData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color).rt.descriptor;
            intermediateTargetDescriptor.width = m_Camera.pixelWidth;
            intermediateTargetDescriptor.height = m_Camera.pixelHeight;
            DoPresentDelayed(context, intermediateTargetDescriptor, k_CameraTarget, true);
        }
    }
}
#endif
