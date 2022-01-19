using System;
using UnityEngine;
#if CLUSTER_DISPLAY_HDRP
using UnityEngine.Assertions;
using UnityEngine.Rendering.HighDefinition;

#elif CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    [Flags]
    enum RenderFeature
    {
        None = 0,
        AsymmetricProjection = 1 << 0,
        ScreenCoordOverride = 1 << 1,
        AsymmetricProjectionAndScreenCoordOverride = AsymmetricProjection | ScreenCoordOverride,
        All = ~0
    }
}
