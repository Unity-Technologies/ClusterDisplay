using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

public class ResetSim : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.frameCount % 100000 == 0)
        {
            foreach (var resetPosition in GameObject.FindObjectsOfType<ResetPosition>())
            {
                resetPosition.gameObject.transform.position = resetPosition.position;
                resetPosition.gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
            }
        }
    }
}
