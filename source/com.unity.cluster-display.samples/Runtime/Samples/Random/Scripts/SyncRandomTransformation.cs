using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

public class SyncRandomTransformation : MonoBehaviour
{
    [SerializeField] private int seed = 123456;

    private float currentUpAngle = 0f;
    
    private float newYAngle = 0f;
    private float currentYAngle = 0f;

    private float newYPosition = 0f;
    private float currentYPosition = 0f;

    // Every 2 seconds, pick a new Y angle to rotate to.
    private IEnumerator NewAngle()
    {
        var wait = new WaitForSeconds(2f);
        
        // If your gonna start a coroutine from Start/Awake() that needs to be synchronized
        // between both the emitter and repeater. You'll need to yield the emitter coroutine
        // by one frame since the emitter essentially runs one frame ahead of the rest of the
        // cluster.
        if (ClusterDisplayState.IsEmitter)
            yield return null;
        
        while (true)
        {
            newYAngle = (Random.value * 2 - 1) * 180f;
            yield return wait;
        }
    }
    
    // Every 3 seconds, pick a new y position to move to.
    private IEnumerator NewPosition()
    {
        var wait = new WaitForSeconds(3f);
        
        // If your gonna start a coroutine from Start/Awake() that needs to be synchronized
        // between both the emitter and repeater. You'll need to yield the emitter coroutine
        // by one frame since the emitter essentially runs one frame ahead of the rest of the
        // cluster.
        if (ClusterDisplayState.IsEmitter)
            yield return null;
        
        while (true)
        {
            newYPosition = Random.Range(-3, 3f);
            yield return wait;
        }
    }

    private void Start ()
    {
        // No need to set the initial seed as that information is communicated automatically between the emitter and repeater.
        // Random.InitState(seed);
        
        StartCoroutine(NewAngle());
        StartCoroutine(NewPosition());
    }

    private void Update()
    {
        currentYAngle = Mathf.Lerp(currentYAngle, newYAngle, Time.deltaTime / 0.1f);
        currentUpAngle += Time.deltaTime * 10f;
        
        transform.rotation = Quaternion.AngleAxis(currentYAngle, Vector3.up) * Quaternion.AngleAxis(currentUpAngle, Vector3.right);

        currentYPosition = Mathf.Lerp(currentYPosition, newYPosition, Time.deltaTime / 0.1f);
        transform.position = new Vector3(transform.position.x, currentYPosition, transform.position.z);
    }
}
