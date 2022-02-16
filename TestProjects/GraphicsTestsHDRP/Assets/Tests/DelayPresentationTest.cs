using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DelayPresentationPresentationTest : BaseDelayPresentationTest
{
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
}
