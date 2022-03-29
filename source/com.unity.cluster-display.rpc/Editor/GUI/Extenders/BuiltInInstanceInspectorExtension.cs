using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public abstract class BuiltInInstanceInspectorExtension<InstanceType> : BuiltInInspectorExtension, IInspectorExtension<InstanceType>
        where InstanceType : Component
    {
        public BuiltInInstanceInspectorExtension(bool useDefaultInspector) : base(useDefaultInspector) {}

        protected virtual void OnExtendInspectorGUI(InstanceType instance) {}
        protected abstract void OnPollWrapperGUI(InstanceType instance, bool anyStreamablesRegistered);

        public void ExtendInspectorGUI(InstanceType instance) => OnExtendInspectorGUI(instance);
        public void PollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered) => OnPollWrapperGUI(instance, anyStreamablesRegistered);

        public override void OnInspectorGUI()
        {
            if (UseDefaultInspector)
                if (DefaultEditorInstance != null)
                    DefaultEditorInstance.OnInspectorGUI();

            // Calling up to base class, which will call down via ExtendInspectorGUI and PollReflectorGUI.
            base.ExtendInspectorGUIWithClusterDisplay<IInspectorExtension<InstanceType>, InstanceType>(this);
        }
    }
}
