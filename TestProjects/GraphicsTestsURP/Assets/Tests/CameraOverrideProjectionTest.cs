using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CameraOverrideProjectionTest : ClusterRendererPostProcessTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("CameraOverrideProjection");
    }

    static IEnumerable<string> VolumeProfileNames => Utils.VolumeProfileNames;

    // Note that LensDistortion is not in this collection.
    // Overscan does its job of removing artefacts at the edge,
    // but the vanilla capture will retain the artefact making the test fail.
    static IEnumerable<string> VolumeProfileOverscanSupportNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "CustomPostProcess";
            yield return "Vignette";
        }
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster([ValueSource("VolumeProfileNames")] string profileName)
    {
        var exceptionHandler = profileName == "FilmGrain" ? () => Debug.LogError("Film grain test requires the LWRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.") : (Action)null;

        yield return RenderAndCompare(() =>
        {
            var cameraTransform = m_Camera.transform;
            var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
            Assert.That(projection, Is.Not.Null);
            projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
            projection.Position = cameraTransform.position;
            projection.Rotation = cameraTransform.rotation;
            projection.ProjectionMatrix = m_Camera.projectionMatrix;
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
            
            var cameraTransform = m_Camera.transform;
            var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
            Assert.That(projection, Is.Not.Null);
            projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
            projection.Position = cameraTransform.position;
            projection.Rotation = cameraTransform.rotation;
            projection.ProjectionMatrix = m_Camera.projectionMatrix;
            m_Volume.profile = LoadVolumeProfile(profileName);
        });
    }
}
