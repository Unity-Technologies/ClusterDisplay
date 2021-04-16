﻿#if CLUSTER_DISPLAY_HDRP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class HDRPClusterRendererModule : ClusterRenderer.IClusterRendererModule, ClusterRenderer.IClusterRendererEventReceiver
    {
        public void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
        }

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
        }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
        }

        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
        }

        public void OnSetCustomLayout(LayoutBuilder layoutBuilder)
        {
            if (ClusterRenderer.LayoutModeIsXR(layoutBuilder.LayoutMode))
                XRSystem.SetCustomLayout((layoutBuilder as IXRLayoutBuilder).BuildLayout);
            else XRSystem.SetCustomLayout(null);
        }
    }
}
#endif
