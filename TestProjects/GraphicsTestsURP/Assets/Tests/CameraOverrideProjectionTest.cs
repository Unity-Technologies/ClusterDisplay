using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics;
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
    public IEnumerator CompareVanillaAndOverrideProjection([ValueSource("VolumeProfileNames")] string profileName)
    {
        InitializeTest();

        var exceptionHandler = profileName == "FilmGrain" ? () => Debug.LogError("Film grain test requires the LWRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.") : (Action)null;

        yield return GraphicsTestUtil.PreWarm();

        PostWarmupInit();

        // Set up the projection with the override properties
        var cameraTransform = m_ReferenceCamera.transform;
        var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
        Assert.That(projection, Is.Not.Null);
        projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
        projection.Position = cameraTransform.position;
        projection.Rotation = cameraTransform.rotation;
        projection.ProjectionMatrix = m_ReferenceCamera.projectionMatrix;
        m_Volume.profile = LoadVolumeProfile(profileName);

        // First we render "vanilla". Use the Reference Camera
        // to render.
        m_Camera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(false);
        m_ReferenceCamera.gameObject.SetActive(true);

        yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

        // Then we activate Cluster Display.
        m_ClusterRenderer.gameObject.SetActive(true);
        m_Camera.gameObject.SetActive(true);

        Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

        yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

        // Even though the cluster camera and the reference camera have
        // different properties, the OverrideProjection should
        // make the cluster camera render as if it has the same properties
        // as the override camera
        AssertClusterAndVanillaAreSimilar(exceptionHandler);
        DisposeTest();
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndOverrideProjectionWithOverscan([ValueSource("VolumeProfileOverscanSupportNames")] string profileName)
    {
        InitializeTest();

        yield return GraphicsTestUtil.PreWarm();

        PostWarmupInit();

        // Set up the projection with the override properties
        var cameraTransform = m_ReferenceCamera.transform;
        var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
        Assert.That(projection, Is.Not.Null);
        projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
        projection.Position = cameraTransform.position;
        projection.Rotation = cameraTransform.rotation;
        projection.ProjectionMatrix = m_ReferenceCamera.projectionMatrix;
        m_Volume.profile = LoadVolumeProfile(profileName);

        m_ClusterRenderer.Settings.OverScanInPixels = 64;

        // First we render "vanilla". Use the Reference Camera
        // to render.
        m_Camera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(false);
        m_ReferenceCamera.gameObject.SetActive(true);

        yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

        // Then we activate Cluster Display.
        m_ClusterRenderer.gameObject.SetActive(true);
        m_Camera.gameObject.SetActive(true);

        Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

        yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

        // Even though the cluster camera and the reference camera have
        // different properties, the OverrideProjection should
        // make the cluster camera render as if it has the same properties
        // as the override camera
        AssertClusterAndVanillaAreSimilar(null);

        DisposeTest();
    }

    [TearDown]
    public void TearDown()
    {
        DisposeTest();
    }
}
