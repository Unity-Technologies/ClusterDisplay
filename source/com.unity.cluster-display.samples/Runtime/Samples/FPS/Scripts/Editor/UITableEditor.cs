using Unity.FPS.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.EditorExt
{
    // The Editor for the UITable component to add an Update button

    [CustomEditor(typeof(UITable), true)]
    public class UITableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UITable myTarget = (UITable)target;
            DrawDefaultInspector();

            if (GUILayout.Button("Update"))
            {
                myTarget.UpdateTable(null);
            }
        }
    }
}
