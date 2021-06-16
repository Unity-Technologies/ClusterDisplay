using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public abstract class UserInspectorExtension<T> : InspectorExtension, IInspectorExtension<T> where T : MonoBehaviour
    {
        protected abstract void OnExtendInspectorGUI(T instance);
        public void ExtendInspectorGUI(T instance) => OnExtendInspectorGUI(instance);
        public override void OnInspectorGUI ()
        {
            base.OnInspectorGUI();
            PollExtendInspectorGUI<IInspectorExtension<T>, T>(this);
        }
    }
}
