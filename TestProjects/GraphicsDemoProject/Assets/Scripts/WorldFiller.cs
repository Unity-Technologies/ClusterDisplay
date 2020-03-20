using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.ClusterRendering.Toolkit
{
    // PERFORMANCE DOES NOT MATTER HERE
    // a simple script filling a test scene with a bunch of prefabs,
    // spares us building a heavyweight scene
    [ExecuteInEditMode]
    public class WorldFiller : MonoBehaviour
    {
        [SerializeField]
        int numInstances;
        [SerializeField]
        GameObject[] prefabs;
        [SerializeField]
        float radius;
        [SerializeField]
        float m_MinScale;
        [SerializeField]
        float m_MaxScale;
        [SerializeField]
        int m_Seed;
        void OnEnable()
        {
            Populate();
        }

        void OnDisable()
        {
            Clear();
        }

        [ContextMenu("Clear")]
        void Clear()
        {
            var children = new List<Transform>();
            foreach (Transform child in transform)
                children.Add(child);
            foreach (var child in children)
                DestroyImmediate(child.gameObject);
        }

        [ContextMenu("Populate")]
        void Populate()
        {
            Clear();
            Populate(numInstances);
        }

        void Populate(int numInstances)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogError("Prefabs array is empty, could not populate scene.");
                return;
            }

            Random.seed = m_Seed;
            for (var i = 0; i != numInstances; ++i)
            {
                var prefabIndex = i % prefabs.Length;
                var obj = Instantiate(prefabs[prefabIndex], transform);
                obj.hideFlags = HideFlags.DontSave;
                obj.transform.localScale = Vector3.one * Mathf.Lerp(m_MinScale, m_MaxScale, Random.value);
                obj.transform.rotation = Random.rotation;
                obj.transform.localPosition = Random.onUnitSphere * radius;
            }
        }
    }
}
