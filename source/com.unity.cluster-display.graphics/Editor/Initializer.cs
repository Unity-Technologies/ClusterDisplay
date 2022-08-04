using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [InitializeOnLoad]
    class Initializer
    {
        static Initializer()
        {
            if (Util.AddAlwaysIncludedShaderIfNeeded(GraphicsUtil.k_BlitShaderName))
            {
                Debug.Log($"Added {GraphicsUtil.k_BlitShaderName} to the list of Always Included shader.");
            }

            if (Util.AddAlwaysIncludedShaderIfNeeded(GraphicsUtil.k_WarpShaderName))
            {
                Debug.Log($"Added {GraphicsUtil.k_WarpShaderName} to the list of Always Included shader.");
            }

            // Sanity check.
            if (XRSettings.enabled)
            {
                Debug.LogWarning("XR is currently enabled which is not expected when using Cluster Display.");
            }
        }
    }
}
