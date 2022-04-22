using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Runtime.CompilerServices;
using System;

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Graphics.Editor")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC.Editor")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Helpers.Editor")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Samples.Editor")]

namespace Unity.ClusterDisplay
{
    internal static class ClusterDisplaySetup
    {
        internal delegate void OnSetupComponents(GameObject gameObject);
        internal static OnSetupComponents onSetupComponents;

        internal delegate void RegisterExpectedComponents (List<Type> expectedComponents);
        internal static RegisterExpectedComponents registerExpectedComponents;

        internal delegate void OnAddedComponent(Type componentType, Component instance);
        internal static OnAddedComponent onAddedComponent;

        private static readonly List<Type> expectedTypes = new List<Type>()
        {
            typeof(ClusterDisplayBootstrap)
        };

        [MenuItem("Cluster Display/Setup Cluster Display")]
        internal static void SetupComponents ()
        {
            registerExpectedComponents?.Invoke(expectedTypes);
            Type[] types = expectedTypes.Where(type => type != null).Distinct().ToArray();

            bool[] instanceExistences = new bool[types.Length];
            UnityEngine.Object[][] instances = new UnityEngine.Object[types.Length][];

            for (int ti = 0; ti < types.Length; ti++)
            {
                instances[ti] = GameObject.FindObjectsOfType(types[ti], true);
                instanceExistences[ti] = instances[ti].Length != 0;
            }

            if (instanceExistences.Any(instanceExistence => instanceExistence))
            {
                var typesStr = types.Where(expectedType => instanceExistences[Array.IndexOf(types, expectedType)])
                    .Select(expectedType => expectedType.FullName)
                    .Aggregate((previous, next) => $"{previous},\r\n\t{next}");

                Debug.LogError($"Cannot setup Cluster Display, there are already instances of the following types that have to be deleted, before setting up Cluster Display again:\r\n\t{typesStr}");
                Selection.objects = instances.SelectMany(typeInstances => typeInstances.Select(typeInstance => (typeInstance as Component).gameObject)).ToArray();

                for (int i = 0; i < Selection.objects.Length; i++)
                {
                    EditorGUIUtility.PingObject(Selection.objects[i]);
                }

                return;
            }

            var clusterDisplayGo = new GameObject("ClusterDisplay");
            for (int ti = 0; ti < types.Length; ti++)
            {
                Debug.Assert(!types[ti].IsAssignableFrom(typeof(Component)), $"The type: \"{types[ti]}\" does not derrive from \"{nameof(Component)}\".");
                var instance = clusterDisplayGo.AddComponent(types[ti]);
                onAddedComponent?.Invoke(types[ti], instance);
            }

            Debug.Log($"Successfully setup Cluster Display components.");
            onSetupComponents?.Invoke(clusterDisplayGo);
            EditorGUIUtility.PingObject(clusterDisplayGo);
        }
    }
}
