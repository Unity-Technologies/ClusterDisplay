using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.Networking;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public interface IInspectorExtension<InstanceType>
    {
        void ExtendInspectorGUI(InstanceType instance);
    }

    public abstract class InspectorExtension : UnityEditor.Editor
    {
        private bool foldOut = false;

        /// <summary>
        /// This method generically casts our target to our instance type then performs
        /// base functions such as determining whether the cluster display foldout should be shown.
        /// </summary>
        /// <typeparam name="EditorType">The custom editor type for our instance.</typeparam>
        /// <typeparam name="InstanceType">The instance type we are extending the inspector for.</typeparam>
        /// <param name="interfaceInstance"></param>
        protected void PollExtendInspectorGUI<EditorType, InstanceType> (EditorType interfaceInstance)
            where EditorType : IInspectorExtension<InstanceType>
            where InstanceType : Object
        {
            var instance = target as InstanceType;
            if (instance == null)
                return;

            if (foldOut = EditorGUILayout.Foldout(foldOut, "Cluster Display"))
                interfaceInstance.ExtendInspectorGUI(instance);
        }

        protected bool TryGetReflectorInstance<ReflectorType, InstanceType> (InstanceType instance, ref ReflectorType reflectorInstance) 
            where InstanceType : Component 
            where ReflectorType : ComponentReflector<InstanceType>
        {
            if (reflectorInstance == null)
            {
                var reflectors = instance.gameObject.GetComponents<ReflectorType>();
                foreach (var reflector in reflectors)
                {
                    if (reflector.TargetInstance != instance)
                        continue;

                    reflectorInstance = reflector;
                    return true;
                }
            }

            return reflectorInstance != null;
        }
    }
}
