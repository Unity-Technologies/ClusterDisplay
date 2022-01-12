using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;

[ExecuteAlways]
public class TileProjectionTest : ClusterRendererTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("TileProjection");
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster()
    {
        InitializeTest();

        yield return Render();

        CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
        CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

        ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);

        DisposeTest();
    }
}
