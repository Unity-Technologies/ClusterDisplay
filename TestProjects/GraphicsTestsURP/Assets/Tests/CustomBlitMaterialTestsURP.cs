using System.Collections;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CustomBlitMaterialTestURP : CustomBlitMaterialTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("CustomBlitMaterial");
    }

    [TearDown]
    public void URPTearDown() => TearDown();

    [UnityTest]
    public IEnumerator URPCustomBlitMaterial()
    {
        yield return TestCustomBlitMaterial();
    }

    [UnityTest]
    public IEnumerator URPCustomBlitMaterialWithMaterialPropertyBlock()
    {
        InitializeTest();
        yield return TestCustomBlitMaterialWithMaterialPropertyBlock();
    }

    [UnityTest]
    public IEnumerator URPUseBlitMaterialWithMaterialPropertyBlocks()
    {
        yield return TestCustomBlitMaterialWithMaterialPropertyBlocks();
    }
}
