using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SchoolTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    public Vector3 Position => target.position;
    public Quaternion Rotation => target.rotation;

    public void PropagateTransform (Vector3 position, Quaternion rotation)
    {
        target.position = position;
        target.rotation = rotation;
    }
}
