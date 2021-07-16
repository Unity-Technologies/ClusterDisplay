using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public abstract class UserInspectorExtension<InstanceType> : InspectorExtension, IInspectorExtension<InstanceType>
        where InstanceType : MonoBehaviour
    {
        protected abstract void OnExtendInspectorGUI(InstanceType instance);
        protected abstract void OnPollWrapperGUI(InstanceType instance, bool anyStreamablesRegistered);

        public void PollReflectorGUI(InstanceType instance, bool hasRegistered) => OnPollWrapperGUI(instance, hasRegistered);
        public void ExtendInspectorGUI(InstanceType instance) => OnExtendInspectorGUI(instance);

        public override void OnInspectorGUI ()
        {
            if (UseDefaultInspector)
                base.OnInspectorGUI();

            PollExtendInspectorGUI<IInspectorExtension<InstanceType>, InstanceType>(this);
        }
    }
}
