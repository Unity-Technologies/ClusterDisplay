using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CameraOverrideProjectionTest : ClusterRendererTestReferenceCamera
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
            yield return "Vignette";
        }
    }

    [UnityTest]
    public IEnumerator CompareReferenceAndCameraOverride([ValueSource("VolumeProfileNames")] string profileName)
    {
        yield return CompareReferenceAndCluster(profileName, () =>
            {
                // Set up the projection with the override properties
                var cameraTransform = m_ReferenceCamera.transform;
                var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
                Assert.That(projection, Is.Not.Null);
                projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
                projection.Position = cameraTransform.position;
                projection.Rotation = cameraTransform.rotation;
                projection.ProjectionMatrix = m_ReferenceCamera.projectionMatrix;
            },
            profileName == "FilmGrain"
                ? () => Debug.LogError(
                    "Film grain test requires the LWRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.")
                : null);
    }

    [UnityTest]
    public IEnumerator CompareReferenceAndCameraOverrideWithOverscan([ValueSource("VolumeProfileOverscanSupportNames")] string profileName)
    {
        yield return CompareReferenceAndCluster(profileName, () =>
        {
            m_ClusterRenderer.Settings.OverScanInPixels = 64;

            // Set up the projection with the override properties
            var cameraTransform = m_ReferenceCamera.transform;
            var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
            Assert.That(projection, Is.Not.Null);
            projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
            projection.Position = cameraTransform.position;
            projection.Rotation = cameraTransform.rotation;
            projection.ProjectionMatrix = m_ReferenceCamera.projectionMatrix;
        });
    }
}
