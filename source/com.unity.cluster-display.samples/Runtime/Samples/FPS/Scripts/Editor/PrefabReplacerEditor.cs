﻿using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEditor;

namespace Unity.FPS.EditorExt
{

    [CustomEditor(typeof(PrefabReplacer))]
    public class PrefabReplacerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Replace"))
            {
                Replace((target as PrefabReplacer));
            }
        }

        public void Replace(PrefabReplacer replacer)
        {
            List<GameObject> allPrefabObjectsInScene = new List<GameObject>();
            foreach (Transform t in GameObject.FindObjectsOfType<Transform>())
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                {
                    allPrefabObjectsInScene.Add(t.gameObject);
                }
            }

            foreach (GameObject go in allPrefabObjectsInScene)
            {
                GameObject instanceSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
                foreach (var replacement in replacer.Replacements)
                {
                    GameObject source = replacer.SwitchOrder ? replacement.TargetPrefab : replacement.SourcePrefab;
                    GameObject target = replacer.SwitchOrder ? replacement.SourcePrefab : replacement.TargetPrefab;

                    if (instanceSource == source)
                    {
                        // Create the instance
                        GameObject instance = PrefabUtility.InstantiatePrefab(target) as GameObject;
                        instance.transform.SetParent(go.transform.parent);
                        instance.transform.position = go.transform.position;
                        instance.transform.rotation = go.transform.rotation;
                        instance.transform.localScale = go.transform.localScale;

                        Undo.RegisterCreatedObjectUndo(instance, "prefab replace");
                        Undo.DestroyObjectImmediate(go);
                    }
                }
            }
        }
    }
}