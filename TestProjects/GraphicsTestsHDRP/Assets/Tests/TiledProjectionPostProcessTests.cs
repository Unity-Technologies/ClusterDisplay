using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;
using UnityObject = UnityEngine.Object;

[ExecuteAlways]
public class TiledProjectionPostProcessTest : ClusterRendererTest
{
    const string k_VolumeProfilesDirectory = "Assets/Settings/PostEffects";

    Volume m_Volume;

    protected override void InitializeTest()
    {
        base.InitializeTest();
        m_Volume = FindObjectOfType<Volume>();
        Assert.IsNotNull(m_Volume, $"Could not find ${nameof(Volume)}");
    }

    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("TiledProjectionPostProcess");
    }

    static IEnumerable<string> VolumeProfileNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "CustomPostProcess";
            yield return "FilmGrain";
            yield return "LensDistortion";
            yield return "Vignette";
        }
    }

    // Note that FilmGrain is not in this list. Its aspect changes with overscan which is ok.
    // The alternative would be to assume the provided grain texture tiles seamlessly which is not guaranteed.
    static IEnumerable<string> VolumeProfileOverscanSupportNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "CustomPostProcess";
            yield return "LensDistortion";
            yield return "Vignette";
        }
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster([ValueSource("VolumeProfileNames")] string profileName)
    {
        var exceptionHandler = profileName == "FilmGrain" ? () => Debug.LogError("Film grain test requires the HDRP_DEBUG_STATIC_POSTFX scripting symbol to be defined in the Player Settings.") : (Action)null;

        yield return RenderAndCompare(() =>
        {
            Assert.IsTrue(m_ClusterRenderer.Settings.OverScanInPixels == 0, "Expected zero overscan.");
            m_Volume.profile = LoadVolumeProfile(profileName);
        }, null, exceptionHandler);
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedClusterWithOverscan([ValueSource("VolumeProfileOverscanSupportNames")] string profileName)
    {
        yield return RenderAndCompare(() =>
        {
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
            m_Volume.profile = LoadVolumeProfile(profileName);
        });
    }

    static VolumeProfile LoadVolumeProfile(string profileName)
    {
        var path = $"{k_VolumeProfilesDirectory}/{profileName}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        Assert.IsNotNull(profile, $"Could not load volume profile at path \"{path}\"");
        return profile;
    }
}
