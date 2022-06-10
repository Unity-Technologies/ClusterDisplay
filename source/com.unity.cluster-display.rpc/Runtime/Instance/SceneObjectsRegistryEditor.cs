using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.RPC
{
    public partial class SceneObjectsRegistry : SceneSingletonMonoBehaviour<SceneObjectsRegistry>
    {
#if UNITY_EDITOR
        [CustomEditor(typeof(SceneObjectsRegistry))]
        public class SceneObjectsRegistryEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI() => base.OnInspectorGUI();
        }
#endif
    }
}
