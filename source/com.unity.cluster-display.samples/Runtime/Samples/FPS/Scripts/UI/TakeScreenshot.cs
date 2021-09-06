using System.IO;
using Unity.FPS.Game;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class TakeScreenshot : MonoBehaviour
    {
        [Tooltip("Root of the screenshot panel in the menu")]
        public GameObject ScreenshotPanel;

        [Tooltip("Name for the screenshot file")]
        public string FileName = "Screenshot";

        [Tooltip("Image to display the screenshot in")]
        public RawImage PreviewImage;

        CanvasGroup m_MenuCanvas = null;
        Texture2D m_Texture;

        bool m_TakeScreenshot;
        bool m_ScreenshotTaken;
        bool m_IsFeatureDisable;

        string GetPath() => k_ScreenshotPath + FileName + ".png";

        const string k_ScreenshotPath = "Assets/";

        void Awake()
        {
#if !UNITY_EDITOR
        // this feature is available only in the editor
        ScreenshotPanel.SetActive(false);
        m_IsFeatureDisable = true;
#else
            m_IsFeatureDisable = false;

            var gameMenuManager = GetComponent<InGameMenuManager>();
            DebugUtility.HandleErrorIfNullGetComponent<InGameMenuManager, TakeScreenshot>(gameMenuManager, this,
                gameObject);

            m_MenuCanvas = gameMenuManager.MenuRoot.GetComponent<CanvasGroup>();
            DebugUtility.HandleErrorIfNullGetComponent<CanvasGroup, TakeScreenshot>(m_MenuCanvas, this,
                gameMenuManager.MenuRoot.gameObject);

            LoadScreenshot();
#endif
        }

        void Update()
        {
            PreviewImage.enabled = PreviewImage.texture != null;

            if (m_IsFeatureDisable)
                return;

            if (m_TakeScreenshot)
            {
                m_MenuCanvas.alpha = 0;
                ScreenCapture.CaptureScreenshot(GetPath());
                m_TakeScreenshot = false;
                m_ScreenshotTaken = true;
                return;
            }

            if (m_ScreenshotTaken)
            {
                LoadScreenshot();
#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif

                m_MenuCanvas.alpha = 1;
                m_ScreenshotTaken = false;
            }
        }

        public void OnTakeScreenshotButtonPressed()
        {
            m_TakeScreenshot = true;
        }

        void LoadScreenshot()
        {
            if (File.Exists(GetPath()))
            {
                var bytes = File.ReadAllBytes(GetPath());

                m_Texture = new Texture2D(2, 2);
                m_Texture.LoadImage(bytes);
                m_Texture.Apply();
                PreviewImage.texture = m_Texture;
            }
        }
    }
}