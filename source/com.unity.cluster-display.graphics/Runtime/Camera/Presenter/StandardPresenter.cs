using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// In standard (non-XR), we automatically create a UI Canvas
    /// to present a RT, since the cameras are not rendering.
    /// </summary>
    class StandardPresenter : Presenter
    {
        RenderTexture m_PresentRT;

        public override RenderTexture presentRT
        {
            get => m_PresentRT;
            set
            {
                if (m_ClusterCanvas == null)
                    return;

                m_PresentRT = value;
                m_ClusterCanvas.rawImageTexture = m_PresentRT;
            }
        }

        ClusterCanvas m_ClusterCanvas;

        protected override void InitializeCamera(Camera camera)
        {
            if (!ClusterCanvas.TryGetInstance(out var clusterCanvas, false))
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

        public override void Dispose()
        {
            DeinitializeCamera(m_Camera);
        }
    }
}
