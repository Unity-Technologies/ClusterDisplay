using System.Collections;
using UnityEngine;
using UnityEngine.TestTools.Graphics;
using NUnit.Framework;

public class CompareStitchedAgainstVanilla : BaseCustomTest
{
    override public IEnumerator Execute()
    {
        var vanilla = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
        var stitched = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
        
        // First render with cluster renderer deactivated.
        m_Renderer.enabled = false;
        yield return null;

        m_Camera.targetTexture = vanilla;
        m_Camera.Render();
        m_Camera.targetTexture = null;
        yield return null;
        
        // Then render with cluster renderer and stitcher activated, you should get the same output.
        m_Renderer.enabled = true;
        m_Renderer.DebugSettings.EnableStitcher = true;
        yield return null;

        m_Camera.targetTexture = stitched;
        m_Camera.Render();
        m_Camera.targetTexture = null;
        yield return null;
        
        var rect = new Rect(0, 0, vanilla.width, vanilla.height);
        var vanillaTex2D = new Texture2D(vanilla.width, vanilla.height);
        RenderTexture.active = vanilla;
        vanillaTex2D.ReadPixels(rect, 0, 0);
        
        var stitchedTex2D = new Texture2D(vanilla.width, vanilla.height);
        RenderTexture.active = stitched;
        stitchedTex2D.ReadPixels(rect, 0, 0);
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(vanilla);
        RenderTexture.ReleaseTemporary(stitched);
        
        // Make sure images are not equal simply because nothing was rendered
        Assert.IsFalse(Utilities.IsMonochromeStochastic(vanillaTex2D, 64));
        ImageAssert.AreEqual(vanillaTex2D, stitchedTex2D, m_Settings.ImageComparisonSettings);

        Texture2D.DestroyImmediate(vanillaTex2D);
        Texture2D.DestroyImmediate(stitchedTex2D);
    }
}
