using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    static class SceneUtils
    {
        /// <summary>
        /// Find all instances of a Unity type in the scene, including inactive ones.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> FindAllObjectsInScene<T>() where T : Object
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(obj => !(EditorUtility.IsPersistent(obj) || obj.hideFlags.HasFlag(HideFlags.NotEditable) || obj.hideFlags.HasFlag(HideFlags.HideAndDontSave)));
        }
    }
}
