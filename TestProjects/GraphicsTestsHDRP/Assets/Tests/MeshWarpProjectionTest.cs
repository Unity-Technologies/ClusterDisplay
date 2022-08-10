using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MeshWarpProjectionTest : ClusterRendererTest
{
    Material m_SimulatedScreen;
    GameObject m_SimulatorObject;

    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("MeshWarpProjection");
    }

    [TearDown]
    public void TearDown()
    {
        m_SimulatorObject.SetActive(true);
    }

    IEnumerator InitScene()
    {
        m_SimulatorObject = GameObject.Find("ScreenSimulator");
        Assert.IsNotNull(m_SimulatorObject);
        m_SimulatedScreen = m_SimulatorObject.GetComponent<Renderer>().sharedMaterial;

        InitializeTest();

        m_SimulatorObject.SetActive(false);

        yield return GraphicsTestUtil.PreWarm();
    }

    IEnumerator RenderSimulatedScreen()
    {
        // Render the warp result to the game view and save it to a texture
        yield return RenderOverscan();

        // Set the warp result texture to the simulator mesh and show it
        m_SimulatedScreen.SetTexture("_MainTex", m_ClusterCapture);
        m_SimulatorObject.SetActive(true);

        // Render the simulated screen (using a regular camera), we'll denote this as "the cluster capture"
        yield return RenderVanilla();
        GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_ClusterCaptureTex2D);
    }

    [UnityTest]
    public IEnumerator TestSimulatedCurvedScreen()
    {
        yield return InitScene();

        yield return RenderSimulatedScreen();

        // Now render the scene normally (without cluster renderer and without the simulated screen).
        // We'll call this render result the "vanilla"
        m_SimulatorObject.SetActive(false);
        yield return RenderVanilla();

        GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
        ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);

        DisposeTest();
    }

    [UnityTest]
    public IEnumerator TestSimulatedCurvedScreenWithOverscan()
    {
        yield return InitScene();

        m_ClusterRenderer.Settings.OverScanInPixels = 64;
        yield return RenderSimulatedScreen();

        // Now render the scene normally (without cluster renderer and without the simulated screen).
        // We'll call this render result the "vanilla"
        m_SimulatorObject.SetActive(false);
        yield return RenderVanilla();

        GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
        ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);

        DisposeTest();
    }
}
