using UnityEngine;

namespace Unity.FPS.AI
{
    // Component used to override values on start from the NavmeshAgent component in order to change
    // how the agent  is moving
    public class NavigationModule : MonoBehaviour
    {
        [Header("Parameters")] [Tooltip("The maximum speed at which the enemy is moving (in world units per second).")]
        public float MoveSpeed = 0f;

        [Tooltip("The maximum speed at which the enemy is rotating (degrees per second).")]
        public float AngularSpeed = 0f;

        [Tooltip("The acceleration to reach the maximum speed (in world units per second squared).")]
        public float Acceleration = 0f;
    }
}