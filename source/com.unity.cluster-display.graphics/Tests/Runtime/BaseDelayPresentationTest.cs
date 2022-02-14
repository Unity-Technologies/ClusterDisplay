using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public class BaseDelayPresentationTest : MonoBehaviour
    {
        struct MoveAround
        {
            readonly Vector3 m_Up;
            readonly Vector3 m_Forward;
            readonly Vector3 m_Position;
            readonly float m_Amount;

            public MoveAround(Transform transform, float amount)
            {
                m_Up = transform.up;
                m_Forward = transform.forward;
                m_Position = transform.position;
                m_Amount = amount;
            }

            public Vector3 Update(float angle)
            {
                return m_Position + Quaternion.AngleAxis(angle, m_Forward) * m_Up * m_Amount;
            }
        }

        const int k_NumFrames = 12;

        protected Camera m_Camera;
        protected Camera m_RefCamera;
        protected ClusterRenderer m_ClusterRenderer;
        protected RenderTexture[] m_VanillaCapture;
        protected RenderTexture[] m_ClusterCapture;
        protected Texture2D m_VanillaCaptureTex2D;
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

            var refCameraGO = new GameObject("Ref Camera", typeof(Camera))
            {
                hideFlags = HideFlags.DontSave
            };
            m_RefCamera = refCameraGO.GetComponent<Camera>();

            GraphicsTestUtil.SetGameViewSize(
                m_GraphicsTestSettings.ImageComparisonSettings.TargetWidth,
                m_GraphicsTestSettings.ImageComparisonSettings.TargetHeight);
        }

        void DisposeTest()
        {
            CoreUtils.Destroy(m_RefCamera.gameObject);
            CoreUtils.Destroy(m_VanillaCaptureTex2D);
            CoreUtils.Destroy(m_ClusterCaptureTex2D);
            GraphicsUtil.DeallocateIfNeeded(ref m_VanillaCapture);
            GraphicsUtil.DeallocateIfNeeded(ref m_ClusterCapture);
        }

        protected IEnumerator RenderAndCompareSequence()
        {
            InitializeTest();

            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");
            Assert.IsTrue(m_ClusterRenderer.DelayPresentByOneFrame);
            Assert.IsTrue(m_ClusterRenderer.isActiveAndEnabled);

            var moveAround = new MoveAround(m_Camera.transform, 2);

            GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, k_NumFrames, m_Camera.pixelWidth, m_Camera.pixelHeight, "vanilla-capture");
            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, k_NumFrames, m_Camera.pixelWidth, m_Camera.pixelHeight, "cluster-capture");

            m_VanillaCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);
            m_ClusterCaptureTex2D = new Texture2D(m_Camera.pixelWidth, m_Camera.pixelHeight);

            using (var vanillaCaptureSequence = new CameraCaptureSequence(m_RefCamera, m_VanillaCapture))
            using (var clusterCaptureSequence = new CameraCaptureSequence(m_ClusterRenderer.PresentCamera, m_ClusterCapture))
            {
                for (var i = 0; i != k_NumFrames; ++i)
                {
                    m_Camera.transform.position = moveAround.Update(i * 30);
                    CopyCamera();

                    vanillaCaptureSequence.SetIndex(i);
                    clusterCaptureSequence.SetIndex(i);

                    yield return new WaitForEndOfFrame();
                }
            }

            // Make sure the cluster output is delayed by one frame.
            for (var i = 0; i != k_NumFrames - 1; ++i)
            {
                GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture[i], m_VanillaCaptureTex2D);
                GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture[i + 1], m_ClusterCaptureTex2D);

                ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            }
            
            // Make sure the test did not pass by accident. (Aka, the frames did change over time.)
            GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture[2], m_VanillaCaptureTex2D);
            GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture[2], m_ClusterCaptureTex2D);
            GraphicsTestUtil.AssertImagesAreNotEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);
            
            DisposeTest();
        }
        
        protected virtual void CopyCamera()
        {
            m_RefCamera.CopyFrom(m_Camera);
        }
    }
}
