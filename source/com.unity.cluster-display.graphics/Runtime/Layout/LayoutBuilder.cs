using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder
    {
        public interface IClusterRenderer
        {
            ClusterRenderContext Context { get; }
            ClusterCameraController CameraController { get; }
            void OnBuildLayout(Camera camera);
        }

        protected readonly IClusterRenderer m_ClusterRenderer;

        public LayoutBuilder (IClusterRenderer clusterRenderer)
        {
            m_ClusterRenderer = clusterRenderer;
        }

        public abstract void Dispose();
    }
}
