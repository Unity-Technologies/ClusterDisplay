using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Unity.ClusterDisplay.Graphics
{
    /*interface IClusterRendererEventReceiver
    {
        void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
        void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
        void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
        void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
    }*/

    /// <summary>
    /// This component is responsible for managing projection, layout (tile, stitcher),
    /// and Cluster Display specific shader features such as Global Screen Space.
    /// </summary>
    /// <remarks>
    /// We typically expect at most one instance active at a given time.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterRenderer : MonoBehaviour
    {
        // For the time being we ignore any logic to fetch cameras from user scenes.
        // We just serialize a reference. We'll add an alternative to the camera-context-registry later.
        [SerializeField]
        Camera m_Camera;

        ILayoutBuilder m_LayoutBuilder;

        [HideInInspector]
        [SerializeField]
        ClusterRenderContext m_Context = new ClusterRenderContext();

        Presenter m_Presenter = new Presenter();
        // TODO consider having layouts expose their target as opposed to having them update the presenter.
        internal Presenter Presenter => m_Presenter;
        
        public bool IsDebug
        {
            get => m_Context.Debug;
            set => m_Context.Debug = value;
        }

        // TODO sketchy, limits client changes for the time being
        internal ClusterRenderContext Context => m_Context;
        
        /// <summary>
        /// User controlled settings, typically project specific.
        /// </summary>
        public ClusterRendererSettings Settings => m_Context.Settings;

        /// <summary>
        /// Debug mode specific settings, meant to be tweaked from the custom inspector or a debug GUI.
        /// </summary>
        public ClusterRendererDebugSettings debugSettings => m_Context.DebugSettings;

        Matrix4x4 m_OriginalProjectionMatrix = Matrix4x4.identity;

        /// <summary>
        /// Camera projection before its slicing to an asymmetric projection.
        /// </summary>
        public Matrix4x4 originalProjectionMatrix => m_OriginalProjectionMatrix;

#if UNITY_EDITOR

        // we need a clip-to-world space conversion for gizmo
        Matrix4x4 m_ViewProjectionInverse = Matrix4x4.identity;
        ClusterFrustumGizmo m_Gizmo = new ClusterFrustumGizmo();

        void OnDrawGizmos()
        {
            if (enabled)
            {
                m_Gizmo.Draw(m_ViewProjectionInverse, m_Context.GridSize, m_Context.TileIndex);
            }
        }

        void OnGUI()
        {
            if (m_LayoutBuilder != null)
            {
                GUI.DrawTexture(new Rect(0, 0, 256, 256), m_LayoutBuilder.PresentRT);
            }
        }
#endif

        // TODO we'll need a method to configure additionnal camera data for HDRP
        void ____()
        {
            /*if (TryGetPreviousCameraContext(out _))
{
    additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = m_PreviousAsymmetricProjectionSetting;
    additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, m_PreviousAsymmetricProjectionSetting);
    additionalCameraData.customRenderingSettings = m_PreviousCustomFrameSettingsToggled;
}

if (TryGetContextCamera(out var contextCamera) && contextCamera.TryGetComponent(out additionalCameraData))
{
    m_PreviousAsymmetricProjectionSetting = additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection];
    m_PreviousCustomFrameSettingsToggled = additionalCameraData.customRenderingSettings;

    additionalCameraData.customRenderingSettings = true;
    additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.AsymetricProjection] = true;
    additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsymetricProjection, true);
    additionalCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
}*/
        }
        
        void OnEnable()
        {
            // Sync, will change from inspector as well.
            GraphicsUtil.SetShaderKeyword(m_Context.DebugSettings.EnableKeyword);
            SetLayoutMode(m_Context.DebugSettings.LayoutMode);

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        void OnDisable()
        {
            // TODO We assume ONE ClusterRenderer. Enforce it.
            GraphicsUtil.SetShaderKeyword(false);
            SetLayoutMode(LayoutMode.None);
        }

        void OnDestroy()
        {
            m_Presenter.Dispose();
        }

        void LateUpdate()
        {
            TryRenderLayout();
            // TODO not the right time to invoke this.
            TryPresentLayout();
        }

        // TODO temporary functions, while we figure the right time to invoke it.
        void TryRenderLayout()
        {
            if (!(m_Context.GridSize.x > 0 && m_Context.GridSize.y > 0))
            {
                return;
            }

            if (m_Camera != null)
            {
#if UNITY_EDITOR
                m_ViewProjectionInverse = (m_Camera.projectionMatrix * m_Camera.worldToCameraMatrix).inverse;
#endif
                // TODO consider a null-object pattern for layout. It is *not* expected to be null while
                // the cluster-renderer is enabled.
                m_LayoutBuilder?.Render(m_Camera);
            }
        }

        void TryPresentLayout()
        {
            // TODO use null-object.
            if (m_LayoutBuilder != null)
            {
                m_LayoutBuilder.Present();
                m_Presenter.PresentRT = m_LayoutBuilder.PresentRT;
            
                // TODO is it really needed?
#if UNITY_EDITOR
                UnityEditor.SceneView.RepaintAll();
#endif
            }
        }

        internal void SetLayoutMode(LayoutMode newLayoutMode)
        {
            if (m_LayoutBuilder != null && m_LayoutBuilder.LayoutMode == newLayoutMode)
            {
                return;
            }

            if (m_LayoutBuilder != null)
            {
                m_LayoutBuilder.Dispose();
                m_LayoutBuilder = null;
            } 

            switch (newLayoutMode)
            {
                case LayoutMode.None:
                    m_LayoutBuilder = null;
                    break;
                case LayoutMode.StandardTile:
                    m_LayoutBuilder = new TileLayoutBuilder(m_Context);
                    break;
                case LayoutMode.StandardStitcher:
                    m_LayoutBuilder = new StitcherLayoutBuilder(m_Context);
                    break;
                default:
                    throw new Exception($"Unimplemented {nameof(LayoutMode)}: \"{newLayoutMode}\".");
            }
        }
    }
}
