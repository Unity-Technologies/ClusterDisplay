using System.Linq;
using UnityEditor;
using System;
using System.Reflection;
using UnityEngine;
using static Unity.ClusterDisplay.Graphics.Editor.ClusterRendererInspector;

namespace Unity.ClusterDisplay.Graphics
{
    [InitializeOnLoad]
    internal static class ClusterDisplayGraphicsSetup
    {
        private static Type[] expectedTypes = new Type[]
        {
            typeof(ClusterRenderer),
            typeof(ClusterRendererCommandLineUtils)
        };

        static ClusterDisplayGraphicsSetup ()
        {
            ClusterDisplaySetup.registerExpectedComponents += (listOfExpectedTypes) => listOfExpectedTypes.AddRange(expectedTypes);
            ClusterDisplaySetup.onAddedComponent += (addedType, instance) =>
            {
                var clusterRenderer = instance as ClusterRenderer;
                if (clusterRenderer == null)
                {
                    return;
                }

                SetProjectionPolicy(clusterRenderer, typeof(TiledProjection));
                AddMissingClusterCameraComponents();
            };
        }
    }
}
