using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [SerializeField]
    Transform m_Target;

    [SerializeField]
    float m_Speed;

    [SerializeField]
    float m_Radius;

    [SerializeField]
    Vector3 m_Offsets;
    
    void Update()
    {
        var rotation = new Vector3(
            Mathf.PerlinNoise(Time.time * m_Speed, m_Offsets.x) * 360,
            Mathf.PerlinNoise(Time.time * m_Speed + 65416.521f, m_Offsets.y) * 360,
            Mathf.PerlinNoise(Time.time * m_Speed + 16485.321f, m_Offsets.z) * 360);
        transform.position = m_Target.position + (Quaternion.Euler(rotation) * Vector3.forward) * m_Radius;
        transform.LookAt(m_Target);
    }
}
