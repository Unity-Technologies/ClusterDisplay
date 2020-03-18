using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using UnityEngine.SceneManagement;
using Unity.ClusterDisplay.Graphics;

public class ClusterDisplayGraphicsTests
{
    [UnityTest, Category("ClusterDisplay")]
    [PrebuildSetup("SetupGraphicsTestCases")]
    [UseGraphicsTestCases]
    public IEnumerator Run(GraphicsTestCase testCase)
    {
        SceneManager.LoadScene(testCase.ScenePath);

        yield return null;
        
        // If there's a custom test, run it.
        var customTest = Object.FindObjectOfType<BaseCustomTest>();
        if (customTest == null)
            Assert.IsFalse(true, "Couldn't find BaseCustomTest component in scene, expected one.'"); // Custom test is mandatory at the moment.
        else
        {
            var enumerator = customTest.Execute();
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}
