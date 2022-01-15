using System;
using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.EditorTests;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class ClusterRendererTest : MonoBehaviour
    {
        static string k_GameViewSizeName = "Graphic Test";
        const string k_MainCameraTag = "MainCamera";

        protected Camera m_Camera;
        protected ClusterRenderer m_ClusterRenderer;
        protected RenderTexture m_VanillaCapture;
        protected RenderTexture m_ClusterCapture;
        protected Texture2D m_VanillaCaptureTex2D;
        protected Texture2D m_ClusterCaptureTex2D;
        protected GraphicsTestSettings m_GraphicsTestSettings;
        
        protected virtual void InitializeTest()
        {
            m_Camera = FindUniqueMainCamera();
            Assert.IsNotNull(m_Camera, "Could not find main camera.");
            m_ClusterRenderer = FindObjectOfType<ClusterRenderer>(true);
            Assert.IsNotNull(m_ClusterRenderer, $"Could not find {nameof(ClusterRenderer)}");
            m_GraphicsTestSettings = FindObjectOfType<GraphicsTestSettings>();
            Assert.IsNotNull(m_GraphicsTestSettings, "Missing test settings for graphic tests.");
            
            SetGameViewSize(
                m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth, 
                m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight);
        }

        protected virtual void DisposeTest()
        {
            CoreUtils.Destroy(m_VanillaCaptureTex2D);
            CoreUtils.Destroy(m_ClusterCaptureTex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCapture);
        }
        
        protected IEnumerator Render()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            // We assume there's no resize during the execution.
            // TODO Maybe force Game View display and size.

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_ClusterRenderer.gameObject.SetActive(false);

            // TODO Could add tests of the ClusterRenderer control of the Camera state. Separately.
            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                // Let a capture of the stitched output happen.
                yield return new WaitForEndOfFrame();
            }
            
            m_ClusterRenderer.gameObject.SetActive(false);
        }

        protected IEnumerator RenderAndCompare(
            Action preRender = null, 
            Action postRender = null,
            Action exceptionHandler = null)
        {
            InitializeTest();

            if (preRender != null)
            {
                preRender.Invoke();
            }
            
            yield return Render();

            if (postRender != null)
            {
                postRender.Invoke();
            }

            CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            if (exceptionHandler == null)
            {
                ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            }
            else
            {
                try
                {
                    ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
                }
                catch (Exception _)
                {
                    exceptionHandler.Invoke();
                    throw;
                }
            }

            DisposeTest();
        }
        
        static Camera FindUniqueMainCamera()
        {
            var cameras = FindObjectsOfType<Camera>(true);
            foreach (var camera in cameras)
            {
                if (camera.CompareTag(k_MainCameraTag))
                {
                    return camera;
                }
            }

            return null;
        }

        protected static void CopyToTexture2D(RenderTexture source, Texture2D dest)
        {
            Assert.IsTrue(source.width == dest.width);
            Assert.IsTrue(source.height == dest.height);

            var restore = RenderTexture.active;
            RenderTexture.active = source;
            dest.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            dest.Apply();
            RenderTexture.active = restore;
        }
        
        static void SetGameViewSize(int width, int height)
        {
            if (GameViewUtils.SizeExists(GameViewSizeGroupType.Standalone, k_GameViewSizeName))
            {
                GameViewUtils.RemoveCustomSize(GameViewSizeGroupType.Standalone, GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
            }

            GameViewUtils.AddCustomSize(
                GameViewUtils.GameViewSizeType.FixedResolution, 
                GameViewSizeGroupType.Standalone, 
                width, height, k_GameViewSizeName);

            GameViewUtils.SetSize(GameViewUtils.FindSize(GameViewSizeGroupType.Standalone, k_GameViewSizeName));
        }
    }
}
