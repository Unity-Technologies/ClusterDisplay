using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// We don't want to use two cameras to render the scene (One to render the scene, and one to present the RT). Therefore, the only
    /// way to perform a present to the screen without a camera is to use a full screen Canvas and this class gets initialized by
    /// StandardPresenter next to an instance of Canvas.
    /// </summary>
    [ExecuteAlways]
    public class ClusterCanvas : SingletonMonoBehaviour<ClusterCanvas>
    {
        [HideInInspector][SerializeField] private Canvas m_Canvas;
        [HideInInspector][SerializeField] private RawImage m_RawImage;

        public RawImage fullScreenRawImage => m_RawImage;

        /// <summary>
        /// When we create our present render texture in standard tile/XR layout, we apply it
        /// to a RawImage here for presentation.
        /// </summary>
        public RenderTexture rawImageTexture { set => fullScreenRawImage.texture = value; }

        /// <summary>
        /// Setup the canvas for presenting cluster display renders.
        /// </summary>
        protected override void OnAwake()
        {
            // We could probably replace this initialization code with an instantiation of a prefab.
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
                m_RawImage.raycastTarget = false;
            }
        }
    }
}
