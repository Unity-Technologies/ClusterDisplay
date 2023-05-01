using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CycleMaterialOnKey : MonoBehaviour
{
    public Material[] materials = { };
    public int materialIndex;
    int m_MaterialSet = -1;
    Renderer m_Renderer;

    // Start is called before the first frame update
    void Start()
    {
        UpdateMaterial();
    }

    void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (materials.Length > 0)
            {
                materialIndex = (materialIndex + 1) % materials.Length;
                UpdateMaterial();
            }
        }
    }

    void UpdateMaterial()
    {
        if (materialIndex != m_MaterialSet && materialIndex < materials.Length)
        {
            m_Renderer.material = materials[materialIndex];
            m_MaterialSet = materialIndex;
        }
    }
}
