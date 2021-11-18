using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public abstract class MonoBehaviourInspectorExtension<InstanceType> : ClusterDisplayInspectorExtension, IInspectorExtension<InstanceType>
        where InstanceType : MonoBehaviour
    {
        protected virtual void OnExtendInspectorGUI(InstanceType instance) {}
        protected virtual void OnPollWrapperGUI(InstanceType instance, bool anyStreamablesRegistered) {}

        public void PollReflectorGUI(InstanceType instance, bool hasRegistered) => OnPollWrapperGUI(instance, hasRegistered);
        public void ExtendInspectorGUI(InstanceType instance) => OnExtendInspectorGUI(instance);

        public override void OnInspectorGUI ()
        {
            if (UseDefaultInspector)
                base.OnInspectorGUI();

            ExtendInspectorGUIWithClusterDisplay<IInspectorExtension<InstanceType>, InstanceType>(this);
        }
    }
}
