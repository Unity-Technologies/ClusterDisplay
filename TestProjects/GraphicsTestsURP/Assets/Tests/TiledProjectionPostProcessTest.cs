using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

public class TiledProjectionPostProcessTest : ClusterRendererPostProcessTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("TiledProjectionPostProcess");
    }

    static IEnumerable<string> VolumeProfileNames => Utils.VolumeProfileNames;

    static IEnumerable<string> VolumeProfileOverscanSupportNames => Utils.VolumeProfileOverscanSupportNames;

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster([ValueSource("VolumeProfileNames")] string profileName)
    {
        var exceptionHandler = profileName == "FilmGrain" ? () => Debug.LogError("Film grain test requires the LWRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.") : (Action)null;

        yield return RenderAndCompare(() =>
        {
            Assert.IsTrue(m_ClusterRenderer.Settings.OverScanInPixels == 0, "Expected zero overscan.");
            m_Volume.profile = LoadVolumeProfile(profileName);
        }, null, exceptionHandler);
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedClusterWithOverscan([ValueSource("VolumeProfileOverscanSupportNames")]
        string profileName)
    {
        yield return RenderAndCompare(() =>
        {
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
            m_Volume.profile = LoadVolumeProfile(profileName);
        });
    }
    
    
    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedClusterWithOverscanAndPhysicalProperties([ValueSource("VolumeProfileOverscanSupportNames")]
        string profileName)
    {
        yield return RenderAndCompare(() =>
        {
            m_Camera.usePhysicalProperties = true;
            m_Camera.focalLength = 16;
            m_Camera.lensShift = new Vector2(0.2f, -0.12f);
            m_Camera.gateFit = Camera.GateFitMode.Vertical;            m_Camera.focalLength = 22;
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
            m_Volume.profile = LoadVolumeProfile(profileName);
        });
    }
}
