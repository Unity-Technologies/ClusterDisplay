using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    public class DebuggingUGUI : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private void Cache()
        {
            canvas = GetComponent<Canvas>();
        }

        private void OnValidate() => Cache();
        private void Awake() => Cache();

        private void LateUpdate ()
        {
            if (canvas == null)
                return;
            
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
        }
    }
}
