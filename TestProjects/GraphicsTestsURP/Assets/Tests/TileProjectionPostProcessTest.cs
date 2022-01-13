using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using Assert = UnityEngine.Assertions.Assert;
using UnityObject = UnityEngine.Object;

[ExecuteAlways]
public class TileProjectionPostProcessTest : ClusterRendererTest
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
        SceneManager.LoadScene("TileProjectionPostProcess");
    }

    static IEnumerable<string> VolumeProfileNames
    {
        get
        {
            yield return "Bloom";
            yield return "ChromaticAberration";
            yield return "DepthOfField";
            yield return "FilmGrain";
            yield return "LensDistortion";
            yield return "MotionBlur";
            yield return "Vignette";
        }
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster([ValueSource("VolumeProfileNames")] string profileName)
    {
        InitializeTest();

        var path = $"{k_VolumeProfilesDirectory}/{profileName}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        Assert.IsNotNull(profile, $"Could not load volume profile at path \"{path}\"");
        
        m_Volume.profile = profile;

        yield return Render();

        CopyToTexture2D(m_VanillaCapture, m_VanillaCaptureTex2D);
        CopyToTexture2D(m_ClusterCapture, m_ClusterCaptureTex2D);

        ImageAssert.AreEqual(m_VanillaCaptureTex2D, m_ClusterCaptureTex2D, m_GraphicsTestSettings.ImageComparisonSettings);

        DisposeTest();
    }

    static List<T> GetAssetsAtPath<T>(string directory) where T : UnityObject
    {
        if (string.IsNullOrEmpty(directory))
        {
            return new List<T>();
        }

        directory = Path.GetDirectoryName($"{directory}/");

        if (!Directory.Exists(directory))
        {
            return new List<T>();
        }

        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { directory });
        var paths = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid));

        return paths.Select(path => AssetDatabase.LoadAssetAtPath<T>(path)).ToList();
    }
}
