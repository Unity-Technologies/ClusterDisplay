using UnityEngine;

namespace Unity.FPS.Game
{
    // This class contains general information describing an actor (player or enemies).
    // It is mostly used for AI detection logic and determining if an actor is friend or foe
    public class Actor : MonoBehaviour
    {
        [Tooltip("Represents the affiliation (or team) of the actor. Actors of the same affiliation are friendly to each other")]
        public int Affiliation;

        [Tooltip("Represents point where other actors will aim when they attack this actor")]
        public Transform AimPoint;

        ActorsManager m_ActorsManager;

        void Start()
        {
            m_ActorsManager = GameObject.FindObjectOfType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, Actor>(m_ActorsManager, this);

            // Register as an actor
            if (!m_ActorsManager.Actors.Contains(this))
            {
                m_ActorsManager.Actors.Add(this);
            }
        }

        void OnDestroy()
        {
            // Unregister as an actor
            if (m_ActorsManager)
            {
                m_ActorsManager.Actors.Remove(this);
            }
        }
    }
}