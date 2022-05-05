using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public abstract class ClusterRendererTestReferenceCamera : ClusterRendererPostProcessTest
    {
        protected Camera m_ReferenceCamera;

        // Call this after GraphicsUtil.PreWarm
        protected void PostWarmupInit()
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

        protected void AssertClusterAndVanillaAreSimilar(Action exceptionHandler)
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
    }
}
