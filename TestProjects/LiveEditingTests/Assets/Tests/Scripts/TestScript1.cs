using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class TestScript1 : MonoBehaviour
    {
        public interface InterfaceA
        {
        }

        [Serializable]
        public class StructA : InterfaceA
        {
            [SerializeField]
            int m_A;
            [SerializeField]
            float m_B;
        }

        [Serializable]
        public class StructB : InterfaceA
        {
            [SerializeField]
            float m_B;
            [SerializeField]
            string m_C;
        }

        [Serializable]
        public class StructC : InterfaceA
        {
            [SerializeField]
            string m_C;
        }

        [SerializeField]
        StructA[] m_Array;

        [SerializeReference]
        InterfaceA m_SerializedRef = new StructA();

        [SerializeField]
        GameObject m_GO;

        [ContextMenu("A")]
        public void SetA()
        {
            m_SerializedRef = new StructA();
        }

        [ContextMenu("B")]
        public void SetB()
        {
            m_SerializedRef = new StructB();
        }

        [ContextMenu("C")]
        public void SetC()
        {
            m_SerializedRef = new StructC();
        }
    }
}
