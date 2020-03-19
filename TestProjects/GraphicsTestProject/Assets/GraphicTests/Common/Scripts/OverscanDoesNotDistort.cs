using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Graphics;

public class OverscanDoesNotDistort : BaseCustomTest
{
    override public IEnumerator Execute()
    {
        var vanilla = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
        var withOverscan = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);

        // First render without overscan.
        Assert.IsTrue(m_Renderer.enabled);
        m_Renderer.Debug = false;
        m_Renderer.Settings.OverscanInPixels = 0;
        yield return null;
        
        m_Camera.targetTexture = vanilla;
        m_Camera.Render();
        m_Camera.targetTexture = null;
        yield return null;
        
        // Then render with overscan.
        m_Renderer.Settings.OverscanInPixels = 128;
        yield return null;
        
        m_Camera.targetTexture = withOverscan;
        m_Camera.Render();
        m_Camera.targetTexture = null;
        yield return null;
        
        var rect = new Rect(0, 0, vanilla.width, vanilla.height);
        var vanillaTex2D = new Texture2D(vanilla.width, vanilla.height);
        RenderTexture.active = vanilla;
        vanillaTex2D.ReadPixels(rect, 0, 0);
        
        var withOverscanTex2D = new Texture2D(vanilla.width, vanilla.height);
        RenderTexture.active = withOverscan;
        withOverscanTex2D.ReadPixels(rect, 0, 0);
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(vanilla);
        RenderTexture.ReleaseTemporary(withOverscan);
        
        // You should see the same thing.
        ImageAssert.AreEqual(vanillaTex2D, withOverscanTex2D, m_Settings.ImageComparisonSettings);
        Assert.IsTrue(Utilities.IsNotMonochrome(vanillaTex2D));

        Texture2D.DestroyImmediate(vanillaTex2D);
        Texture2D.DestroyImmediate(withOverscanTex2D);
    }
}
