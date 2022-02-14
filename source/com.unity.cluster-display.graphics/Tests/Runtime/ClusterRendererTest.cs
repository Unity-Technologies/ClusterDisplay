using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class ClusterRendererTest : MonoBehaviour
    {
        protected Camera m_Camera;
        protected ClusterRenderer m_ClusterRenderer;
        protected RenderTexture m_VanillaCapture;
        protected RenderTexture m_ClusterCapture;
        protected Texture2D m_VanillaCaptureTex2D;
        protected Texture2D m_VanillaCapture2Tex2D; // Used when making sure camera is restored properly.
        protected Texture2D m_ClusterCaptureTex2D;
        protected GraphicsTestSettings m_GraphicsTestSettings;

        protected virtual void InitializeTest()
        {
            m_Camera = GraphicsTestUtil.FindUniqueMainCamera();
            Assert.IsNotNull(m_Camera, "Could not find main camera.");
            m_ClusterRenderer = FindObjectOfType<ClusterRenderer>(true);
            Assert.IsNotNull(m_ClusterRenderer, $"Could not find {nameof(ClusterRenderer)}");
            m_GraphicsTestSettings = FindObjectOfType<GraphicsTestSettings>();
            Assert.IsNotNull(m_GraphicsTestSettings, "Missing test settings for graphic tests.");

            GraphicsTestUtil.SetGameViewSize(
                m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth,
                m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight);
        }

        protected virtual void DisposeTest()
        {
            CoreUtils.Destroy(m_VanillaCaptureTex2D);
            CoreUtils.Destroy(m_VanillaCapture2Tex2D);
            CoreUtils.Destroy(m_ClusterCaptureTex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCapture);
        }

        protected IEnumerator RenderVanillaAndOverscan()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);

            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                // Let a capture of the stitched output happen.
                yield return new WaitForEndOfFrame();
            }

            m_ClusterRenderer.gameObject.SetActive(false);
        }

        protected IEnumerator CameraIsRestoredProperly(
            Action preRender = null,
            Action postRender = null)
        {
            InitializeTest();

            if (preRender != null)
            {
                preRender.Invoke();
            }

            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_VanillaCapture2Tex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);

            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                // Let a capture of the stitched output happen.
                yield return new WaitForEndOfFrame();
            }

            // We expect vanilla and cluster output to be different.
            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            GraphicsTestUtil.AssertImagesAreNotEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);

            // Then we deactivate Cluster Display.
            // We expect the output to be restored to what it was before Cluster Display was activated.
            m_ClusterRenderer.gameObject.SetActive(false);

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCapture2Tex2D);

            ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_VanillaCapture2Tex2D, m_GraphicsTestSettings.ImageComparisonSettings);

            if (postRender != null)
            {
                postRender.Invoke();
            }

            DisposeTest();
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

            yield return RenderVanillaAndOverscan();

            if (postRender != null)
            {
                postRender.Invoke();
            }

            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

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
                catch (Exception)
                {
                    exceptionHandler.Invoke();
                    throw;
                }
            }

            DisposeTest();
        }

        protected void AlignSurfaceWithCameraFrustum(float nearPlaneOffset)
        {
            var projection = m_ClusterRenderer.ProjectionPolicy as TrackedPerspectiveProjection;
            Assert.IsNotNull(projection);
            Assert.IsTrue(projection.Surfaces.Count == 1);

            var alignedSurface = GraphicsTestUtil.AlignSurfaceWithCameraFrustum(projection.Surfaces[0], m_Camera, nearPlaneOffset, m_ClusterRenderer.transform);
            projection.SetSurface(0, alignedSurface);
        }
    }
}
