using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics;
using UnityEngine.TestTools.Graphics;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.ClusterDisplay.Graphics.Tests.Universal
{
    [ExecuteAlways]
    public class ClusterRendererTest : MonoBehaviour
    {
        [SerializeField]
        Camera m_Camera;

        [SerializeField]
        ClusterRenderer m_ClusterRenderer;

        RenderTexture m_VanillaCapture;
        RenderTexture m_StitcherCapture;
        Texture2D m_VanillaCaptureTex2D;
        Texture2D m_StitcherCapturetex2D;

        void OnGUI()
        {
            if (m_VanillaCapture != null)
            {
                GUI.DrawTexture(new Rect(0, 0, 256, 256), m_VanillaCapture);
            }

            if (m_StitcherCapture != null)
            {
                GUI.DrawTexture(new Rect(256, 0, 256, 256), m_StitcherCapture);
            }
        }

        void OnEnable()
        {
            Blitter_.InitializeIfNeeded();
        }

        void OnDisable()
        {
            Blitter_.Dispose();
            
            CoreUtils.Destroy(m_VanillaCaptureTex2D);
            CoreUtils.Destroy(m_StitcherCapturetex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_StitcherCapture);
            StopAllCoroutines();
        }

        [ContextMenu("Run Test")]
        void RunTest()
        {
            StartCoroutine(CompareVanillaAndStitchedCluster());
        }

        // [UnityTest]
        IEnumerator CompareVanillaAndStitchedCluster()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

            // We assume there's no resize during the execution.
            // TODO Maybe force Game View display and size.

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);
            GraphicsUtil.AllocateIfNeeded(ref m_StitcherCapture, m_Camera.pixelWidth, m_Camera.pixelHeight);

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_StitcherCapturetex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            
            m_ClusterRenderer.gameObject.SetActive(true);
            m_ClusterRenderer.enabled = false;

            // TODO Could add tests of the ClusterRenderer control of the Camera state. Separately.
            m_Camera.gameObject.SetActive(true);
            m_Camera.enabled = true;

            using (new CameraCapture(m_Camera, m_VanillaCapture))
            {
                // Let a capture of the vanilla output happen.
                yield return new WaitForEndOfFrame();
            }

            m_ClusterRenderer.enabled = true;

            Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

            using (new CameraCapture(m_ClusterRenderer.PresentCamera, m_StitcherCapture))
            {
                // Let a capture of the stitched output happen.
                // TODO Figure out why 2 frames are needed.
                yield return new WaitForEndOfFrame();
            }

            // Compare the two.

            yield return null;

            CopyTotexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
            CopyTotexture2D(m_StitcherCapture, m_StitcherCapturetex2D);

            var settings = new ImageComparisonSettings();
            
            ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_StitcherCapturetex2D, settings);
        }

        IEnumerator Wait()
        {
            yield return null;
            yield return null;
        }

        static void CopyTotexture2D(RenderTexture source, Texture2D dest)
        {
            Assert.IsTrue(source.width == dest.width);
            Assert.IsTrue(source.height == dest.height);
            
            var restore = RenderTexture.active;
            RenderTexture.active = source;
            dest.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            dest.Apply();
            RenderTexture.active = restore;
        }
    }
}
