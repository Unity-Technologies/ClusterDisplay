using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder
    {
        public interface ILayoutReceiver
        {
            void OnBuildLayout(Camera camera);
        }

        protected readonly IClusterRenderer m_ClusterRenderer;

        public delegate void OnReceiveLayout(Camera camera);
        protected OnReceiveLayout onReceiveLayout;

        public abstract ClusterRenderer.LayoutMode LayoutMode { get; }

        public LayoutBuilder (IClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;
        public void RegisterOnReceiveLayout (ILayoutReceiver layoutReciever) => onReceiveLayout += layoutReciever.OnBuildLayout;

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
    }
}
