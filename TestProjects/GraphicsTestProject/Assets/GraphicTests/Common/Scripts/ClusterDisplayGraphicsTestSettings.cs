using System;
using UnityEngine.TestTools.Graphics;

public class ClusterDisplayGraphicsTestSettings : GraphicsTestSettings
{
    public ClusterDisplayGraphicsTestSettings()
    {
        ImageComparisonSettings.TargetWidth = 512;
        ImageComparisonSettings.TargetHeight = 512;
        ImageComparisonSettings.AverageCorrectnessThreshold = 0.005f;
        ImageComparisonSettings.PerPixelCorrectnessThreshold = 0.005f;
    }
}
