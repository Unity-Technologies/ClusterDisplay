using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetFramerate : MonoBehaviour
{
    [SerializeField]
    int m_TargetFramerate;

    void OnEnable()
    {
        Application.targetFrameRate = m_TargetFramerate;
        QualitySettings.vSyncCount = 1;
        QualitySettings.maxQueuedFrames = 1;
    }
}
