using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CycleAnimationsOnEmitter : MonoBehaviour
{
    private Animator animator;

    private bool Cache ()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        return animator != null;
    }

    private void OnValidate() => Cache();
    private void Awake()
    {
        if (ClusterDisplayState.IsRepeater)
            return;

        animator.Play("No Weapon Locomotion");
    }

    private float time;
    private float x = 0f, y = 1f;

    private void Update()
    {
        if (!Cache())
            return;

        time += Time.deltaTime * 0.1f;
        float rad = (time / 180f) * Mathf.PI;

        float newX = Mathf.Cos(rad) * x - Mathf.Sin(rad) * y;
        float newY = Mathf.Sin(rad) * x + Mathf.Cos(rad) * y;

        x = newX;
        y = newY;

        animator.SetFloat("Direction", x);
        animator.SetFloat("Speed", y);
    }
}
