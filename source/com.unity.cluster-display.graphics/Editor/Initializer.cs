using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [InitializeOnLoad]
    static class Initializer
    {
        static Initializer()
        {
            // Sanity check.
            if (XRSettings.enabled)
            {
                Debug.LogWarning("XR is currently enabled which is not expected when using Cluster Display.");
            }
        }
    }
}
