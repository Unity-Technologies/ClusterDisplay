using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools.Graphics;

namespace GraphicTests.Common.Scripts
{
    public class OverscanCropsProperly : BaseCustomTest
    {
        static readonly Vector2 k_BorderInPixels = new Vector2(32, 32);
        static readonly Vector2Int k_GridSize = new Vector2Int(1, 1);
        const int k_Overscan = 128;

        [SerializeField]
        CustomPassVolume m_CustomPassVolume;

        BorderCustomPass m_BorderCustomPass;
        BorderCustomPass borderCustomPass
        {
            get
            {
                if (m_BorderCustomPass == null)
                {
                    foreach (var pass in m_CustomPassVolume.customPasses)
                    {
                        if (pass is BorderCustomPass)
                        {
                            m_BorderCustomPass = pass as BorderCustomPass;
                            break;
                        }
                    }
                    if (m_BorderCustomPass == null)
                        Assert.False(true, "Failed to access BorderCustomPass from CustomPassVolume.");
                }
                return m_BorderCustomPass;
            }
        }

        public override IEnumerator Execute()
        {
            var vanilla = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
            var withOverscan = RenderTexture.GetTemporary(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);
            var targetSize = new Vector2(m_Settings.ImageComparisonSettings.TargetWidth, m_Settings.ImageComparisonSettings.TargetHeight);

            // Render without overscan and a border.
            Assert.IsTrue(m_Renderer.enabled);
            m_Renderer.Debug = false;
            m_Renderer.Settings.OverscanInPixels = 0;
            m_Renderer.Settings.GridSize = k_GridSize;
            borderCustomPass.color = Color.green;
            borderCustomPass.borderColor  = Color.red;
            borderCustomPass.gridSize = k_GridSize;
            borderCustomPass.normalizedBorder = new Vector2(k_BorderInPixels.x / targetSize.x, k_BorderInPixels.y / targetSize.y);
            yield return null;
            
            m_Camera.targetTexture = vanilla;
            m_Camera.Render();
            m_Camera.targetTexture = null;
            yield return null;
        
            // Turn overscan up.
            m_Renderer.Settings.OverscanInPixels = k_Overscan;
            // Adjust the border to keep the same aspect.
            var adjustedBorderInPixels = k_BorderInPixels + Vector2.one * k_Overscan;
            var adjustedNormalizedborder = new Vector2(
                adjustedBorderInPixels.x / (targetSize.x + 2 * k_Overscan),
                adjustedBorderInPixels.y / (targetSize.y + 2 * k_Overscan));
            borderCustomPass.normalizedBorder = adjustedNormalizedborder;
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
}
