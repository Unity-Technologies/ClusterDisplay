using System;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor
{
    static class EditorUtils
    {
        public static string GetAssetsFolder()
        {
            const string clusterAssetsPath = "ClusterDisplay";

            if (!AssetDatabase.IsValidFolder($"Assets/{clusterAssetsPath}"))
                AssetDatabase.CreateFolder("Assets", clusterAssetsPath);

            return $"Assets/{clusterAssetsPath}";
        }
    }
}
