using System;
using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ColorSetter : MonoBehaviour
{
    [SerializeField] private Renderer renderer;
    private Material materialInstance;

    private void OnValidate() =>
        renderer = GetComponent<Renderer>();

    [ClusterRPC]
    public void SetColor (Color color)
    {
        Debug.Log($"Setting color: \"{color}\".");
        if (materialInstance == null)
        {
            materialInstance = new Material(renderer.material.shader);
            renderer.material = materialInstance;
        }
        
        materialInstance.SetColor("_BaseColor", color);
    }
}
