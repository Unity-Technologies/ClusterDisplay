using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace Unity.ClusterDisplay.Networking
{
    [InitializeOnLoad]
    public static class InternalRPCILPostProcessor
    {
        static InternalRPCILPostProcessor ()
        {
            return;
            try
            {
                EditorApplication.LockReloadAssemblies();

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    Debug.Log(assembly.FullName);
                }

                EditorApplication.UnlockReloadAssemblies();

            } catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
}
