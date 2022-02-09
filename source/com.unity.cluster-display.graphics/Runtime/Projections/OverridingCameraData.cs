using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [System.Serializable]
    public struct OverridingCameraData
    {
        [SerializeField]
        public OverrideProperty m_Overrides;
        
        [SerializeField]
        public Vector3 m_Position;
        
        [SerializeField]
        public Quaternion m_Rotation;
        
        [SerializeField]
        public Matrix4x4 m_ProjectionMatrix;
    }
}
