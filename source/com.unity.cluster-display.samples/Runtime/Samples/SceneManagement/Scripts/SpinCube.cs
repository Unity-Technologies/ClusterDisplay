using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinCube : MonoBehaviour
{
    private float currentUpAngle = 0f;
    private float currentYAngle = 0f;
    private float currentYPosition = 0f;

    private float accumulator = 0f;

    private void Update()
    {
        accumulator += Time.deltaTime;
        
        currentUpAngle = accumulator * 90f;
        currentYAngle = accumulator * 45f;
        
        currentYPosition = Mathf.Cos(accumulator * Mathf.PI) * 0.5f + 0.5f;
        
        transform.rotation = Quaternion.AngleAxis(currentYAngle, Vector3.up) * Quaternion.AngleAxis(currentUpAngle, Vector3.right);
        transform.position = new Vector3(transform.position.x, currentYPosition, transform.position.z);
    }
}
