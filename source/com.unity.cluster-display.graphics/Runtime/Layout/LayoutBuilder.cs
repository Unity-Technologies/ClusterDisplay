using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder : ClusterRenderer.IClusterRendererEventReceiver
    {
        public interface ILayoutReceiver
        {
            void OnBuildLayout(Camera camera);
        }

        public delegate void OnReceiveLayout(Camera camera);

        public static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);
        protected readonly IClusterRenderer m_ClusterRenderer;
        protected OnReceiveLayout onReceiveLayout;
        protected Rect m_OverscannedRect;
        protected int m_OverscanInPixels;
        protected Vector2 m_DebugScaleBiasTexOffset;

        public abstract ClusterRenderer.LayoutMode LayoutMode { get; }

        public LayoutBuilder (IClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;
        ~LayoutBuilder ()
        {
            Dispose();
        }

        public void RegisterOnReceiveLayout (ILayoutReceiver layoutReciever) => onReceiveLayout += layoutReciever.OnBuildLayout;

        public abstract void OnBeginRender(ScriptableRenderContext context, Camera camera);
        public abstract void LateUpdate();
        public abstract void OnEndRender(ScriptableRenderContext context, Camera camera);

        protected static bool MatrixContainsNaNs (Matrix4x4 matrix)
        {
            return
                float.IsNaN(matrix.m00) ||
                float.IsNaN(matrix.m01) ||
                float.IsNaN(matrix.m02) ||
                float.IsNaN(matrix.m03) ||
                float.IsNaN(matrix.m10) ||
                float.IsNaN(matrix.m11) ||
                float.IsNaN(matrix.m12) ||
                float.IsNaN(matrix.m13) ||
                float.IsNaN(matrix.m20) ||
                float.IsNaN(matrix.m21) ||
                float.IsNaN(matrix.m22) ||
                float.IsNaN(matrix.m23) ||
                float.IsNaN(matrix.m30) ||
                float.IsNaN(matrix.m31) ||
                float.IsNaN(matrix.m32) ||
                float.IsNaN(matrix.m33);
        }

        public abstract void Dispose();

        protected void CalculateParameters (out Vector2 croppedSize, out Vector2 overscannedSize, out Vector4 scaleBias)
        {
            croppedSize = new Vector2(m_OverscannedRect.width - 2 * m_OverscanInPixels, m_OverscannedRect.height - 2 * m_OverscanInPixels);
            overscannedSize = new Vector2(m_OverscannedRect.width, m_OverscannedRect.height);
            scaleBias = new Vector4(
                croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                m_OverscanInPixels / overscannedSize.x, m_OverscanInPixels / overscannedSize.y); // offset
            scaleBias.z += m_DebugScaleBiasTexOffset.x;
            scaleBias.w += m_DebugScaleBiasTexOffset.y;
        }
    }
}
