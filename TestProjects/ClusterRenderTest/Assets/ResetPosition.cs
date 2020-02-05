using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class ResetPosition : MonoBehaviour
    {
        public int numFramesToReset = 100;
        private Vector3 position;
        private int frameCount = 0;
        private bool collideed;
        private void Start()
        {
            position = transform.position;
        }

        private void Update()
        {
            frameCount++;

            if (collideed && frameCount > numFramesToReset)
            {
                collideed = false;
                transform.position = position;
                var rigid = transform.gameObject.GetComponent<Rigidbody>();
                rigid.velocity = new Vector3();
                rigid.angularVelocity = Vector3.ClampMagnitude(rigid.angularVelocity, 1);
                frameCount = 0;
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            collideed = true;
        }
    }
}