using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class ResetPosition : MonoBehaviour
    {
        public Vector3 position;
        private void Start()
        {
            position = transform.position;
        }

        private void OnCollisionEnter(Collision other)
        {
            //transform.position = position;
        }
    }
}