using System;
using UnityEditor;
using UnityEngine;

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
        }
    }
}
