using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CustomBlitMaterialTestsHDRP : CustomBlitMaterialTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("CustomBlitMaterial");
    }

    [TearDown]
    public void HDRPTearDown() => TearDown();

    [UnityTest]
    public IEnumerator HDRPCustomBlitMaterial()
    {
        yield return TestCustomBlitMaterial();
    }

    [UnityTest]
    public IEnumerator HDRPCustomBlitMaterialWithMaterialPropertyBlock()
    {
        InitializeTest();
        yield return TestCustomBlitMaterialWithMaterialPropertyBlock();
    }

    [UnityTest]
    public IEnumerator HDRPCustomBlitMaterialWithMaterialPropertyBlocks()
    {
        InitializeTest();
        yield return TestCustomBlitMaterialWithMaterialPropertyBlock();
    }
}
