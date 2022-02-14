using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DelayPresentationPresentationTest : BaseDelayPresentationTest
{
    HDAdditionalCameraData m_MainAdditionalCameraData;
    HDAdditionalCameraData m_RefAdditionalCameraData;

    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("DelayPresentation");
    }

    [UnityTest]
    public IEnumerator ClusterOutputIsDelayedByOneFrame()
    {
        yield return RenderAndCompareSequence();
    }

    protected override void InitializeTest()
    {
        base.InitializeTest();

        m_MainAdditionalCameraData = m_Camera.GetComponent<HDAdditionalCameraData>();
        Assert.IsNotNull(m_MainAdditionalCameraData);
        m_RefAdditionalCameraData = m_RefCamera.GetComponent<HDAdditionalCameraData>();
        Assert.IsNotNull(m_RefAdditionalCameraData);
    }

    protected override void CopyCamera()
    {
        m_RefCamera.CopyFrom(m_Camera);
        m_MainAdditionalCameraData.CopyTo(m_RefAdditionalCameraData);
    }
}
