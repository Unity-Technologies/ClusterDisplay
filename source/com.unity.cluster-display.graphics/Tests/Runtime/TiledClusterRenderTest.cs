using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    /// <summary>
    /// Specialized version of <see cref="ClusterRendererTest"/> dealing with tiled projection policy that will render
    /// tiles individually and assembling them instead of relying on debug mode stitching that has some limitations with
    /// some post processing effects.
    /// </summary>
    public class TiledClusterRenderTest : ClusterRendererTest
    {
        protected override IEnumerator RenderOverscan()
        {
            Assert.IsNotNull(m_Camera, $"{nameof(m_Camera)} not assigned.");
            Assert.IsNotNull(m_ClusterRenderer, $"{nameof(m_ClusterRenderer)} not assigned.");
            Assert.That(m_ClusterRenderer.ProjectionPolicy, Is.InstanceOf(typeof(TiledProjection)));
            var tiledPolicy = (TiledProjection)m_ClusterRenderer.ProjectionPolicy;

            var width = m_ImageComparisonSettings.TargetWidth;
            var height = m_ImageComparisonSettings.TargetHeight;

            GraphicsUtil.AllocateIfNeeded(ref m_ClusterCapture, width, height);
            m_ClusterCaptureTex2D = new Texture2D(width, height);
            RenderTexture tileCapture = null;
            GraphicsUtil.AllocateIfNeeded(ref tileCapture, width, height);

            // Enable ClusterRenderer to render the camera through cluster display pipeline.
            m_ClusterRenderer.gameObject.SetActive(true);

            try
            {
                Assert.IsNotNull(m_ClusterRenderer.PresentCamera); // FSTL Sortir du try?
                Vector2 tileScale = new(1.0f / (float)tiledPolicy.Settings.GridSize.x,
                    1.0f / (float)tiledPolicy.Settings.GridSize.y);

                // Loop through every tiles
                for (var row = 0; row < tiledPolicy.Settings.GridSize.y; ++row)
                {
                    float rowOffset = (float)row / (float)tiledPolicy.Settings.GridSize.y;
                    for (var column = 0; column < tiledPolicy.Settings.GridSize.x; ++column)
                    {
                        // Draw the tile
                        tiledPolicy.NodeIndexOverride = row * tiledPolicy.Settings.GridSize.x + column;

                        yield return GraphicsTestUtil.DoScreenCapture(tileCapture);

                        // Activate to dump to disk the content of each tile
                        //var directory = System.IO.Path.Combine("Assets/ActualImages", $"{QualitySettings.activeColorSpace}/{Application.platform.ToString()}/{SystemInfo.graphicsDeviceType}/None");
                        //GraphicsTestUtil.CopyToTexture2D(tileCapture, m_ClusterCaptureTex2D);
                        //GraphicsTestUtil.SaveAsPNG(m_ClusterCaptureTex2D, directory, $"tile{column},{row}");

                        // Merge the tile in m_ClusterCapture as the debug stitching of the tile projection policy would
                        // have done.
                        CommandBuffer blitCommands = new();
                        float columnOffset = (float)column / (float)tiledPolicy.Settings.GridSize.x;
                        GraphicsUtil.Blit(blitCommands, tileCapture, new Vector4(1, 1, 0, 0),
                            new Vector4(tileScale.x, tileScale.y, columnOffset, rowOffset), false);
                        var previousActive = RenderTexture.active;
                        RenderTexture.active = m_ClusterCapture;
                        try
                        {
                            UnityEngine.Graphics.ExecuteCommandBuffer(blitCommands);
                        }
                        finally
                        {
                            RenderTexture.active = previousActive;
                        }
                    }
                }
            }
            finally
            {
                m_ClusterRenderer.gameObject.SetActive(false);
            }
        }
    }
}
