using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    [ExecuteAlways]
    public class ClusterCanvas : SingletonMonoBehaviour<ClusterCanvas>
    {
        [HideInInspector]
        [SerializeField]
        Canvas m_Canvas;
        [HideInInspector]
        [SerializeField]
        RawImage m_RawImage;

        public RawImage fullScreenRawImage => m_RawImage;

        public RenderTexture rawImageTexture
        {
            set => fullScreenRawImage.texture = value;
        }

        void Awake()
        {
            if (m_Canvas == null)
            {
                m_Canvas = gameObject.AddComponent<Canvas>();
                m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                m_Canvas.pixelPerfect = false;

                var canvasScaler = m_Canvas.gameObject.AddComponent<CanvasScaler>();
                canvasScaler.scaleFactor = 1;
                canvasScaler.referencePixelsPerUnit = 100;
            }

            if (m_RawImage == null)
            {
                var rawImageGo = new GameObject("ClusterPresentRawImage");
                rawImageGo.transform.SetParent(m_Canvas.transform);

                m_RawImage = rawImageGo.AddComponent<RawImage>();
                m_RawImage.rectTransform.anchorMin = Vector2.zero;
                m_RawImage.rectTransform.anchorMax = Vector2.one;
                m_RawImage.rectTransform.offsetMin = Vector2.zero;
                m_RawImage.rectTransform.offsetMax = Vector2.zero;
            }
        }
    }
}
