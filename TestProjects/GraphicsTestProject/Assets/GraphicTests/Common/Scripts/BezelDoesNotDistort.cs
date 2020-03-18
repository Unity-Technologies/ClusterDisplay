using System.Collections;
using NUnit.Framework;
using UnityEngine;

public class BezelDoesNotDistort : BaseCustomTest
{
    override public IEnumerator Execute()
    {
        var vanilla = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
        var stitched = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
           
        // Vanilla render.
        m_Renderer.enabled = false;
        yield return null;
        
        m_Camera.targetTexture = vanilla;
        m_Camera.Render();
        m_Camera.targetTexture = null;
        yield return null;

        // Render with stitcher and bezel.
        m_Renderer.enabled = true;
        m_Renderer.Debug = true;
        m_Renderer.DebugSettings.EnableStitcher = true;
        m_Renderer.Settings.PhysicalScreenSize = Vector2Int.one * 512;
        m_Renderer.Settings.Bezel = Vector2Int.one * 32;
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

        Assert.IsTrue(Utilities.NonZeroPixelsAreEqual(stitchedTex2D, vanillaTex2D, 0.02f));
        Assert.IsTrue(Utilities.IsNotMonochrome(vanillaTex2D));

        Texture2D.DestroyImmediate(vanillaTex2D);
        Texture2D.DestroyImmediate(stitchedTex2D);
    }
}