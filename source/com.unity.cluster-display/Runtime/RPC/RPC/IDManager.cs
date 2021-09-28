using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class IDManager
    {
        public const ulong MaxIDCount = ushort.MaxValue;

        private ushort[] m_QueuedReturnedIDs = new ushort[MaxIDCount];
        private bool[] m_ReturnedIDs = new bool[MaxIDCount];
        private ushort m_ReturnedIDsIndex = 0;

        private bool[] m_IDStates = new bool[MaxIDCount];

        private ushort m_NewIDIndex = 0;

        public bool TryPopId(out ushort id)
        {
            id = 0;
            if (m_ReturnedIDsIndex > 0)
            {
                id = (ushort)(m_QueuedReturnedIDs[--m_ReturnedIDsIndex]);
                m_ReturnedIDs[id] = false;
            }

            else if (m_NewIDIndex < MaxIDCount)
            {
                id = m_NewIDIndex++;

                // Not great, maybe I should just use dictionaries.
                while (m_IDStates[id])
                {
                    id = m_NewIDIndex++;
                    if (id >= MaxIDCount)
                        goto allIDsInUse;
                }
            }

            else goto allIDsInUse;

            m_IDStates[id] = true;
            return true;

            allIDsInUse:
            Debug.LogError($"All ids are in use.");
            return false;
        }

        /// <summary>
        /// When you push an ID, you should not push an ID that was previously used by this
        /// ID manager. It's structured this way so I don't need to search m_QueuedReturnedIDs
        /// for whether the ID we want to push is currently in the return queue.
        /// </summary>
        /// <param name="id">The ID we want to register as in use.</param>
        /// <returns></returns>
        public bool TryPushId(ushort id, bool throwError = true)
        {
            if (m_ReturnedIDs[id])
            {
                if (throwError)
                    Debug.LogError(
                        $"The ID: {id} was used previously, so you should be using TryPopId instead due to internal structure.");
                return false;
            }

            if (m_IDStates[id])
            {
                if (throwError)
                    Debug.LogError($"The ID: {id} is already in use.");
                return false;
            }

            m_IDStates[id] = true;
            return true;
        }

        /// <summary>
        /// Attempt to push an ID as being used, if it's in use, then try 
        /// and pop a new one. If that also fails, return false.
        /// </summary>
        /// <param name="id">The ID we are pushing or returning if we pop a new one.</param>
        /// <returns></returns>
        public bool TryPushOrPopId(ref ushort id) => TryPushId(id, throwError: false) || TryPopId(out id);

        public void PushUnutilizedId(ushort id)
        {
            m_IDStates[id] = false;
            m_QueuedReturnedIDs[m_ReturnedIDsIndex++] = id;
            m_ReturnedIDs[id] = true;
        }

        public void Clear()
        {
            m_QueuedReturnedIDs = new ushort[MaxIDCount];
            m_ReturnedIDs = new bool[MaxIDCount];
            m_IDStates = new bool[MaxIDCount];

            m_ReturnedIDsIndex = 0;
            m_NewIDIndex = 0;
        }
    }
}