using UnityEngine;

namespace Unity.FPS.Game
{
    public class ConstantRotation : MonoBehaviour
    {
        [Tooltip("Rotation angle per second")] public float RotatingSpeed = 360f;

        void Update()
        {
            // Handle rotating
            transform.Rotate(Vector3.up, RotatingSpeed * Time.deltaTime, Space.Self);
        }
    }
}