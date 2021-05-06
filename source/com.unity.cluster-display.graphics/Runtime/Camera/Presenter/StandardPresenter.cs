using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardPresenter : Presenter
    {
        private RenderTexture m_PresentRT;
        public override RenderTexture PresentRT 
        { 
            get => m_PresentRT;
            set
            {
                if (m_ClusterCanvas == null)
                    return;

                m_PresentRT = value;
                m_ClusterCanvas.RawImageTexture = m_PresentRT;
            } 
        }

        private ClusterCanvas m_ClusterCanvas;

        protected override void InitializeCamera(Camera camera)
        {
            if (!ClusterCanvas.TryGetInstance(out var clusterCanvas, throwException: false))
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
