using System;
using UnityEngine;

// simplistic camera controller, demonstrate interactivity
public class CameraRotationController : MonoBehaviour
{
    [SerializeField]
    float m_RotationSpeed;

    Quaternion m_BaseRotation;
    Vector3 m_RotationEuler;

    const float k_HalfRange = 15;
    
    void OnEnable()
    {
        m_RotationEuler = Vector3.zero;
        m_BaseRotation = transform.rotation;
    }

    void Update()
    {
        var dx = 0f;
        var dy = 0f;
        if (Input.GetKey(KeyCode.RightArrow))
            dy += 1;
        if (Input.GetKey(KeyCode.LeftArrow))
            dy -= 1;
        if (Input.GetKey(KeyCode.UpArrow))
            dx -= 1;
        if (Input.GetKey(KeyCode.DownArrow))
            dx += 1;

        m_RotationEuler.x = Mathf.Clamp(m_RotationEuler.x + dx * m_RotationSpeed * Time.deltaTime, -k_HalfRange, k_HalfRange);
        m_RotationEuler.y = Mathf.Clamp(m_RotationEuler.y + dy * m_RotationSpeed * Time.deltaTime, -k_HalfRange, k_HalfRange);
        transform.rotation = Quaternion.Euler(m_RotationEuler) * m_BaseRotation;
    }
}
