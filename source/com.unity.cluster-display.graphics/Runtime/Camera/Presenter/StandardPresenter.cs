using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// In standard (non-XR), we automatically create a UI Canvas
    /// to present a RT, since the cameras are not rendering.
    /// </summary>
    public class StandardPresenter : Presenter
    {
        private RenderTexture m_PresentRT;
        private RenderTexture m_BackBuffer;

        public override RenderTexture presentRT 
        {
            get => m_PresentRT;
            set
            {
                if (m_ClusterCanvas == null)
                    return;

                m_ClusterCanvas.rawImageTexture = value;
                m_PresentRT = value;
            } 
        }

        private ClusterCanvas m_ClusterCanvas;

        protected override void InitializeCamera(Camera camera)
        {
            if (!ClusterCanvas.TryGetInstance(out var clusterCanvas, logError: false))
                m_ClusterCanvas = new GameObject("ClusterCanvas").AddComponent<ClusterCanvas>();
            else m_ClusterCanvas = clusterCanvas.GetComponent<ClusterCanvas>();

            if (Application.isPlaying)
                Object.DontDestroyOnLoad(m_ClusterCanvas.gameObject);
        }

        protected override void DeinitializeCamera(Camera camera)
        {
            if (m_ClusterCanvas != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(m_ClusterCanvas.gameObject);
                else Object.DestroyImmediate(m_ClusterCanvas.gameObject);
            }
        }

        public override void Dispose() => DeinitializeCamera(m_Camera);
    }
}
