using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    public class SpinAroundRandomAxis : MonoBehaviour
    {
        private int numFrames = 0;
        Vector3 axis = new Vector3(1,0,0);
        
        private void Update()
        {
            numFrames++;
            if (numFrames > 1000)
            {
                axis = new Vector3(Random.value, Random.value, Random.value);
                numFrames = 0;
            }
            
            gameObject.transform.localRotation *= Quaternion.AngleAxis(3,axis); 
        }
    }
}