using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Fish : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private TextMesh textMesh;
    public Rigidbody RB
    {
        get
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            return rb;
        }
    }

    [SerializeField] private float speed = 0.5f;
    public float Speed => speed;

    public Vector3 torqueAxis;
    public string FishName { get => textMesh.text; set => textMesh.text = value; }

    private void OnValidate()
    {
        textMesh = GetComponentInChildren<TextMesh>();
        rb = GetComponent<Rigidbody>();
    }
}
