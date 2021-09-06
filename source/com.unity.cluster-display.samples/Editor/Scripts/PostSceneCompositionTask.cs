using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ClusterDisplay
{
    public abstract class PostSceneCompositionTask : ScriptableObject
    {
        public abstract void Execute(SceneAsset sceneAsset, Scene openedScene);
    }
}
