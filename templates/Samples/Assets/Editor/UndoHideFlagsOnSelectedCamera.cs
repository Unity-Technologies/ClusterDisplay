using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class UndoHideFlagsOnSelectedCamera
{
    [MenuItem("Unity/Undo Hide Flags on Selected Camera")]
    private static void Undo ()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
            return;

        var firstObject = Selection.objects[0];

        var go = firstObject as GameObject;
        if (go == null)
            return;

        var camera = go.GetComponent<Camera>();
        if (camera == null)
            return;

        camera.gameObject.hideFlags = HideFlags.None;
        camera.hideFlags = HideFlags.None;
    }
}
