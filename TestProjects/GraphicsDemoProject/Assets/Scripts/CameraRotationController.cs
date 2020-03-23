﻿using System;
using UnityEngine;
using Random = UnityEngine.Random;

// simplistic camera controller, demonstrate interactivity
public class CameraRotationController : MonoBehaviour
{
    [SerializeField]
    Transform m_LookAt;
   
    [SerializeField]
    float m_RotationSpeed;

    [SerializeField]
    float m_DistanceToTarget;

    [SerializeField]
    float m_SwitchToAutomaticTimeout;
    
    [SerializeField]
    float m_JumpTimeout;
    
    [SerializeField]
    float m_PerlinScrollSpeed;

    float m_Yaw;
    float m_Pitch;
    float m_LastInputTimestamp;
    float m_LastJumpTimestamp;
    float m_PitchPerlinOffset;
    float m_AutoTime;
    
    const float k_PitchRange = 60;

    void OnGUI()
    {
        GUILayout.Label("Use arrows to move the camera. Press J to jump around.");
    }
    
    void OnEnable()
    {
        // Will force auto control mode on Update.
        m_LastInputTimestamp = float.NegativeInfinity;
        // Do not jump right away.
        m_AutoTime = Time.time;
        m_LastJumpTimestamp = m_AutoTime;
    }

    void Update()
    {
        // Input direction.
        var dPitch = 0f;
        var dYaw = 0f;
        if (Input.GetKey(KeyCode.RightArrow))
            dYaw -= 1;
        if (Input.GetKey(KeyCode.LeftArrow))
            dYaw += 1;
        if (Input.GetKey(KeyCode.UpArrow))
            dPitch -= 1;
        if (Input.GetKey(KeyCode.DownArrow))
            dPitch += 1;

        // We may jump following a keystroke or a timeout.
        var jump = Input.GetKeyDown(KeyCode.J);

        if (dPitch != 0 || dYaw != 0 || jump)
        {
            m_LastInputTimestamp = Time.time;
        }
        else if (Time.time - m_LastInputTimestamp > m_SwitchToAutomaticTimeout)
        {
            // Auto: move along the sphere using Perlin noise.
            dYaw = (Mathf.PerlinNoise(Time.time * m_PerlinScrollSpeed, 0) - 0.5f) * 2;
            dPitch = (Mathf.PerlinNoise(Time.time * m_PerlinScrollSpeed, m_PitchPerlinOffset) - 0.5f) * 2;
            // Increment automatic mode time.
            m_AutoTime += Time.deltaTime;
            jump |= m_AutoTime - m_LastJumpTimestamp > m_JumpTimeout;
        }

        // Jump to try out camera cuts.
        if (jump)
        {
            m_Yaw = Random.Range(0, 360);
            m_Pitch = Random.value * -k_PitchRange;
            m_PitchPerlinOffset = Random.value * 128.0f;
            m_LastJumpTimestamp = m_AutoTime;
        }
        else
        {
            // Regular movement.
            m_Pitch += dPitch * m_RotationSpeed / Time.deltaTime;
            m_Yaw += dYaw * m_RotationSpeed / Time.deltaTime;
        }

        // Note that pitch is bounded to make usage more intuitive.
        m_Pitch = Mathf.Clamp(m_Pitch, -k_PitchRange, 0);
        transform.position = m_LookAt.position + Quaternion.AngleAxis(m_Yaw, Vector3.up) * Quaternion.AngleAxis(m_Pitch, Vector3.right) * Vector3.forward * m_DistanceToTarget;
        transform.LookAt(m_LookAt);
    }
}
