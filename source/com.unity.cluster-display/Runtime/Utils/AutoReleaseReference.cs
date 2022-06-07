using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    public class AutoReleaseReference<T> where T : class, IDisposable
    {
        T m_Disposable;
        int m_RefCount;
        Func<T> m_CreateFunc;

        public class SharedRef : IDisposable
        {
            AutoReleaseReference<T> m_Ref;

            public SharedRef(AutoReleaseReference<T> autoReleaseRef)
            {
                m_Ref = autoReleaseRef;
                m_Ref.Reserve();
            }

            T Value => m_Ref.m_Disposable;

            public void Dispose()
            {
                m_Ref.Release();
            }

            public static implicit operator T(SharedRef @ref) => @ref?.Value;
        }

        public AutoReleaseReference(Func<T> createFunc)
        {
            m_CreateFunc = createFunc;
        }

        public SharedRef GetReference()
        {
            return new SharedRef(this);
        }

        void Reserve()
        {
            if (m_Disposable == null)
            {
                m_Disposable = m_CreateFunc();
            }

            m_RefCount++;
        }

        void Release()
        {
            if (m_Disposable == null) return;

            m_RefCount--;
            if (m_RefCount == 0)
            {
                m_Disposable.Dispose();
                m_Disposable = null;
            }
        }
    }
}
