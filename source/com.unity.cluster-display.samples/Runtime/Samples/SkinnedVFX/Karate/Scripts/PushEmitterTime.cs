using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay.RPC;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class PushEmitterTime : MonoBehaviour
{
    [SerializeField] private VisualEffect visualEffect;

    private void OnValidate() => 
        visualEffect = GetComponent<VisualEffect>();

    [ClusterRPC]
    public void PushTime (float time) => 
        visualEffect.SetFloat("Time", time);

    private void LateUpdate() => 
        PushTime(Time.time);
}
