using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TiledProjectionCameraRestoreTests : ClusterRendererPostProcessTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("TiledProjectionPostProcess");
    }

    static IEnumerable<string> VolumeProfileNames => Utils.VolumeProfileNames;

    [UnityTest]
    public IEnumerator CameraIsRestoredProperly([ValueSource("VolumeProfileNames")] string profileName)
    {
        bool restoreDebug = false;
        
        yield return CameraIsRestoredProperly(() =>
        {
            restoreDebug = m_ClusterRenderer.IsDebug;
            m_ClusterRenderer.IsDebug = false; // No stitcher.
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
            m_Volume.profile = LoadVolumeProfile(profileName);
        },() =>
        {
            m_ClusterRenderer.IsDebug = restoreDebug; // Must be restored
        });
    }
    
    [UnityTest]
    public IEnumerator CameraIsRestoredProperlyUsingPhysicalProperties([ValueSource("VolumeProfileNames")] string profileName)
    {
        bool restoreDebug = false;
        
        yield return CameraIsRestoredProperly(() =>
        {
            restoreDebug = m_ClusterRenderer.IsDebug;
            m_Camera.usePhysicalProperties = true;
            m_Camera.focalLength = 16;
            m_Camera.lensShift = new Vector2(0.2f, -0.12f);
            m_Camera.gateFit = Camera.GateFitMode.Vertical;
            m_ClusterRenderer.IsDebug = false; // No stitcher.
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
            m_Volume.profile = LoadVolumeProfile(profileName);
        },() =>
        {
            m_ClusterRenderer.IsDebug = restoreDebug; // Must be restored
        });
    }
}
