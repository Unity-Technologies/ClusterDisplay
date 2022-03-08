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
        protected ImageComparisonSettings m_ImageComparisonSettings;

        protected virtual void InitializeTest()
        {
            m_Camera = GraphicsTestUtil.FindUniqueMainCamera();
            Assert.IsNotNull(m_Camera, "Could not find main camera.");
            m_ClusterRenderer = FindObjectOfType<ClusterRenderer>(true);
            Assert.IsNotNull(m_ClusterRenderer, $"Could not find {nameof(ClusterRenderer)}");
            m_ImageComparisonSettings = FindObjectOfType<ImageComparisonSettings>();
            Assert.IsNotNull(m_ImageComparisonSettings, "Missing image comparison settings.");

            EditorBridge.OpenGameView();
            
            var success = EditorBridge.SetGameViewSize(
                m_ImageComparisonSettings.TargetWidth,
                m_ImageComparisonSettings.TargetHeight);
            Assert.IsTrue(success);
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

            var width = m_ImageComparisonSettings.TargetWidth;
            var height = m_ImageComparisonSettings.TargetHeight;
                
            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, width, height);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, width, height);

            m_VanillaCaptureTex2D = new Texture2D(width, height);
            m_ClusterCaptureTex2D = new Texture2D(width, height);

            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);
            
            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

            m_ClusterRenderer.gameObject.SetActive(false);
        }

        protected IEnumerator CameraIsRestoredProperly(
            Action preRender = null,
            Action postRender = null)
        {
            InitializeTest();

            yield return GraphicsTestUtil.PreWarm();

            if (preRender != null)
            {
                preRender.Invoke();
            }
            
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");
            
            var width = m_ImageComparisonSettings.TargetWidth;
            var height = m_ImageComparisonSettings.TargetHeight;

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture,  width, height);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, width, height);

            m_VanillaCaptureTex2D = new Texture2D(width, height);
            m_VanillaCapture2Tex2D = new Texture2D(width, height);
            m_ClusterCaptureTex2D = new Texture2D(width, height);

            // First we render "vanilla", that is, without Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(false);

            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

            // We expect vanilla and cluster output to be different.
            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            _ImageAssert.AreNotEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);

            // Then we deactivate Cluster Display.
            // We expect the output to be restored to what it was before Cluster Display was activated.
            m_ClusterRenderer.gameObject.SetActive(false);

            yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCapture2Tex2D);

            _ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_VanillaCapture2Tex2D, m_ImageComparisonSettings);

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
            
            yield return GraphicsTestUtil.PreWarm();

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
                _ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
            }
            else
            {
                try
                {
                    _ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
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
