using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    public abstract class NestedInspector : UnityEditor.Editor
    {
        public virtual void OnSceneGUI()
        {
        }
    }
}