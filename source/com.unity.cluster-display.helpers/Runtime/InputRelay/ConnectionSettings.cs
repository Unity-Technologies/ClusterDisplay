using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Helpers
{
    [System.Serializable]
    internal struct ConnectionSettings
    {
        [SerializeField] public string emitterAddress;
        [SerializeField] public int port;

        public ConnectionSettings (string emitterAddress, int port)
        {
            this.emitterAddress = emitterAddress;
            this.port = port;
        }
    }
}
