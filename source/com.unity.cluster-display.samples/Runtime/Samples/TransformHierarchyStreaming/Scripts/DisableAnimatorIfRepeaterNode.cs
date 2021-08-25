using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class DisableAnimatorIfRepeaterNode : MonoBehaviour
{
    private Animator animator;
    private void OnValidate() =>
        animator = GetComponent<Animator>();

    private void OnEnable() =>
        animator.enabled = !ClusterDisplayState.IsClusterLogicEnabled || !ClusterDisplayState.IsRepeater;
}
