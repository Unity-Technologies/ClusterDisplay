using System.Linq;
using UnityEditor;
using System;

namespace Unity.ClusterDisplay.Graphics
{
    [InitializeOnLoad]
    internal static class ClusterDisplayGraphicsSetup
    {
        private static Type[] expectedTypes = new Type[]
        {
            typeof(ClusterRenderer)
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

                var firstProjectionPolicy = AssetDatabase
                    .FindAssets($"t:{typeof(ProjectionPolicy).Name}")
                    .Select(guid => AssetDatabase
                        .LoadAssetAtPath<ProjectionPolicy>(AssetDatabase
                        .GUIDToAssetPath(guid))).FirstOrDefault();

                clusterRenderer.ProjectionPolicy = firstProjectionPolicy;
            };
        }
    }
}
