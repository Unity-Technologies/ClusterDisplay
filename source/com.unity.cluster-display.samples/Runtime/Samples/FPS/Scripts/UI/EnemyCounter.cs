using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class EnemyCounter : MonoBehaviour
    {
        [Header("Enemies")] [Tooltip("Text component for displaying enemy objective progress")]
        public Text EnemiesText;

        EnemyManager m_EnemyManager;

        void Awake()
        {
            m_EnemyManager = FindObjectOfType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyCounter>(m_EnemyManager, this);
        }

        void Update()
        {
            EnemiesText.text = m_EnemyManager.NumberOfEnemiesRemaining + "/" + m_EnemyManager.NumberOfEnemiesTotal;
        }
    }
}