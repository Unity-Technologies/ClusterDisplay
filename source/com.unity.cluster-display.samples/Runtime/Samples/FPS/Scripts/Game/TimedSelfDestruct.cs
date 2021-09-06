using UnityEngine;

namespace Unity.FPS.Game
{
    public class TimedSelfDestruct : MonoBehaviour
    {
        public float LifeTime = 1f;

        float m_SpawnTime;

        void Awake()
        {
            m_SpawnTime = Time.time;
        }

        void Update()
        {
            if (Time.time > m_SpawnTime + LifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
}