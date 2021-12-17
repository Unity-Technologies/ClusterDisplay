using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    /// <summary>
    /// Provides hooks to Editor events so that they may be
    /// called manually.
    /// </summary>
    /// <remarks>
    /// A typical use case would be for embedding a custom inspector
    /// inside another custom inspector.
    /// </remarks>
    public abstract class NestedInspector : UnityEditor.Editor
    {
        public virtual void OnSceneGUI()
        {
        }
    }
}