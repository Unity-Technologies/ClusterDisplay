using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public abstract class ClusterRendererTestReferenceCamera : ClusterRendererPostProcessTest
    {
        protected Camera m_ReferenceCamera;

        [TearDown]
        public void TearDown()
        {
            DisposeTest();
        }

        protected IEnumerator CompareReferenceAndCluster(string profileName, Action setUpProjection, Action exceptionHandler = null)
        {
            InitializeTest();

            yield return GraphicsTestUtil.PreWarm();

            PostWarmupInit();
            setUpProjection();

            yield return RenderAndCompare(profileName, exceptionHandler);
        }

        // Call this after GraphicsUtil.PreWarm
        void PostWarmupInit()
        {
            m_ReferenceCamera = GameObject.Find("ReferenceCamera").GetComponent<Camera>();

            Assert.NotNull(m_ReferenceCamera);
            m_ReferenceCamera.gameObject.SetActive(false);

            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            var width = m_ImageComparisonSettings.TargetWidth;
            var height = m_ImageComparisonSettings.TargetHeight;

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, width, height);
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, width, height);

            m_VanillaCaptureTex2D = new Texture2D(width, height);
            m_ClusterCaptureTex2D = new Texture2D(width, height);
        }

        void AssertClusterAndVanillaAreSimilar(Action exceptionHandler)
        {
            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

            if (exceptionHandler == null)
            {
                ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
            }
            else
            {
                try
                {
                    ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
                }
                catch (Exception)
                {
                    exceptionHandler.Invoke();
                    throw;
                }
            }
        }

        IEnumerator RenderAndCompare(string profileName, Action exceptionHandler = null)
        {
            m_Volume.profile = LoadVolumeProfile(profileName);

            // First we render "vanilla". Use the Reference Camera
            // to render.
            m_Camera.gameObject.SetActive(false);
            m_ClusterRenderer.gameObject.SetActive(false);
            m_ReferenceCamera.gameObject.SetActive(true);

            yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

            // Then we activate Cluster Display.
            m_ClusterRenderer.gameObject.SetActive(true);
            m_Camera.gameObject.SetActive(true);

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

            // Even though the cluster camera and the reference camera have
            // different properties, the projection logic should
            // make the cluster camera render the same image as the reference.
            AssertClusterAndVanillaAreSimilar(exceptionHandler);
        }
    }
}
