using Unity.ClusterDisplay.Graphics.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [InitializeOnLoad]
    class Initializer
    {
        const string k_BlitShaderName = "ClusterDisplay/Blit";
        
        static Initializer()
        {
            if (Util.AddAlwaysIncludedShaderIfNeeded(k_BlitShaderName))
            {
                Debug.Log($"Added {k_BlitShaderName} to the list of Always Included shader.");
            }
        }
    }
}
