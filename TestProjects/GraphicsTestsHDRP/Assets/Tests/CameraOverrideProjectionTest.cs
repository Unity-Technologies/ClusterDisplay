using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CameraOverrideProjectionTest : ClusterRendererPostProcessTest
{
    // Contains the properties that we want to apply to the cluster render
    Camera m_ReferenceCamera;

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

    [SetUp]
    public void SetUp()
    {
        InitializeTest();
        m_ReferenceCamera = GameObject.Find("ReferenceCamera").GetComponent<Camera>();

        Assert.NotNull(m_ReferenceCamera);
        m_ReferenceCamera.gameObject.SetActive(false);

        Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
        Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");

        var width = m_ImageComparisonSettings.TargetWidth;
        var height = m_ImageComparisonSettings.TargetHeight;

        GraphicsUtil.AllocateIfNeeded(ref m_VanillaCapture, width, height);
        GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, width, height);

        m_VanillaCaptureTex2D = new Texture2D(width, height);
        m_ClusterCaptureTex2D = new Texture2D(width, height);
    }

    void AssertClusterAndVanillaAreSimilar(Action exceptionHandler)
    {
        GraphicsTestUtil.CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
        GraphicsTestUtil.CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

        if (exceptionHandler == null)
        {
            ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
        }
        else
        {
            try
            {
                ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_ImageComparisonSettings);
            }
            catch (Exception)
            {
                exceptionHandler.Invoke();
                throw;
            }
        }
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndOverrideProjection([ValueSource("VolumeProfileNames")] string profileName)
    {
        var exceptionHandler = profileName == "FilmGrain" ? () => Debug.LogError("Film grain test requires the LWRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.") : (Action)null;

        yield return GraphicsTestUtil.PreWarm();

        // Set up the projection with the override properties
        var cameraTransform = m_ReferenceCamera.transform;
        var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
        Assert.That(projection, Is.Not.Null);
        projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
        projection.Position = cameraTransform.position;
        projection.Rotation = cameraTransform.rotation;
        projection.ProjectionMatrix = Matrix4x4.Perspective(m_ReferenceCamera.fieldOfView, 1, m_ReferenceCamera.nearClipPlane, m_ReferenceCamera.farClipPlane);
        m_Volume.profile = LoadVolumeProfile(profileName);

        // First we render "vanilla". Use the Reference Camera
        // to render.
        m_Camera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(false);
        m_ReferenceCamera.gameObject.SetActive(true);

        yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

        // Then we activate Cluster Display.
        m_ReferenceCamera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(true);
        m_Camera.gameObject.SetActive(true);

        Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

        yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

        // Even though the cluster camera and the reference camera have
        // different properties, the OverrideProjection should
        // make the cluster camera render as if it has the same properties
        // as the override camera
        AssertClusterAndVanillaAreSimilar(exceptionHandler);
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndOverrideProjectionWithOverscan([ValueSource("VolumeProfileOverscanSupportNames")]
        string profileName)
    {
        yield return GraphicsTestUtil.PreWarm();

        m_ClusterRenderer.Settings.OverScanInPixels = 64;

        // Set up the projection with the override properties
        var cameraTransform = m_ReferenceCamera.transform;
        var projection = m_ClusterRenderer.ProjectionPolicy as CameraOverrideProjection;
        Assert.That(projection, Is.Not.Null);
        projection.Overrides = CameraOverrideProjection.OverrideProperty.All;
        projection.Position = cameraTransform.position;
        projection.Rotation = cameraTransform.rotation;
        projection.ProjectionMatrix = Matrix4x4.Perspective(m_ReferenceCamera.fieldOfView, 1, m_ReferenceCamera.nearClipPlane, m_ReferenceCamera.farClipPlane);
        m_Volume.profile = LoadVolumeProfile(profileName);


        // First we render "vanilla". Use the Reference Camera
        // to render.
        m_Camera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(false);
        m_ReferenceCamera.gameObject.SetActive(true);

        yield return GraphicsTestUtil.DoScreenCapture(m_VanillaCapture);

        // Then we activate Cluster Display.
        m_ReferenceCamera.gameObject.SetActive(false);
        m_ClusterRenderer.gameObject.SetActive(true);
        m_Camera.gameObject.SetActive(true);

        Assert.IsNotNull(m_ClusterRenderer.PresentCamera);

        yield return GraphicsTestUtil.DoScreenCapture(m_ClusterCapture);

        // Even though the cluster camera and the reference camera have
        // different properties, the OverrideProjection should
        // make the cluster camera render as if it has the same properties
        // as the override camera
        AssertClusterAndVanillaAreSimilar(null);
    }

    [TearDown]
    public void TearDown()
    {
        m_ReferenceCamera.gameObject.SetActive(true);
        DisposeTest();
    }
}
