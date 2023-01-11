#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.LowLevel;

namespace Unity.ClusterDisplay.Scripting
{
    struct InputEventReader
    {
        InputEventTrace m_EventTrace;

        public InputEventReader(InputEventTrace eventTrace)
        {
            m_EventTrace = eventTrace;
        }

        public InputEventEnumerator GetEnumerator() => new InputEventEnumerator(m_EventTrace);
    }

    struct InputEventEnumerator
    {
        InputEventTrace m_EventTrace;
        InputEventPtr m_Current;

        public InputEventEnumerator(InputEventTrace eventTrace)
        {
            m_EventTrace = eventTrace;
            m_Current = default;
        }

        public InputEventPtr Current => m_Current;

        public bool MoveNext() => m_EventTrace.GetNextEvent(ref m_Current);
    }

    static class InputEventTraceExtensions
    {
        public static bool TryGetDeviceInfo(this InputEventTrace eventTrace, int deviceId, out InputEventTrace.DeviceInfo deviceInfo)
        {
            deviceInfo = default;
            foreach (var info in eventTrace.deviceInfos)
            {
                if (info.deviceId == deviceId)
                {
                    deviceInfo = info;
                    return true;
                }
            }

            return false;
        }
    }
}

#endif
