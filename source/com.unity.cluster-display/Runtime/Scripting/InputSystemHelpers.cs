#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Allows foreach enumeration of <see cref="InputEventTrace"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="InputEventTrace"/> implements <see cref="IEnumerable{T}"/>, but it requires GC allocation.
    /// </remarks>
    struct InputEventReader
    {
        InputEventTrace m_EventTrace;

        public InputEventReader(InputEventTrace eventTrace) => m_EventTrace = eventTrace;

        public InputEventEnumerator GetEnumerator() => new(m_EventTrace);
    }

    /// <summary>
    /// Alloc-free enumerator for <see cref="InputEventTrace"/>.
    /// </summary>
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
        /// <summary>
        /// Retrieve the <see cref="InputEventTrace.DeviceInfo"/> from the event trace with the specified the device ID.
        /// </summary>
        /// <param name="eventTrace">The event trace.</param>
        /// <param name="deviceId">The ID of the device to get.</param>
        /// <param name="deviceInfo">When this method returns, contains the <see cref="InputEventTrace.DeviceInfo"/>
        /// associated with the device ID, if the device is found.</param>
        /// <returns>true if the device with the ID was found.</returns>
        public static bool TryGetDeviceInfo(this InputEventTrace eventTrace, int deviceId, out InputEventTrace.DeviceInfo deviceInfo)
        {
            foreach (var info in eventTrace.deviceInfos)
            {
                if (info.deviceId == deviceId)
                {
                    deviceInfo = info;
                    return true;
                }
            }

            deviceInfo = default;
            return false;
        }
    }

    readonly struct SavedInputSettings
    {
        public readonly InputSettings.UpdateMode UpdateMode;
        public readonly InputSettings.BackgroundBehavior BackgroundBehavior;

        public SavedInputSettings(InputSettings.UpdateMode updateMode,
            InputSettings.BackgroundBehavior backgroundBehavior)
        {
            UpdateMode = updateMode;
            BackgroundBehavior = backgroundBehavior;
        }
    }

    /// <summary>
    /// Methods for saving and restoring select properties in <see cref="InputSettings"/>.
    /// </summary>
    static class InputSettingsExtensions
    {
        public static SavedInputSettings Save(this InputSettings settings) =>
            new(settings.updateMode,
                settings.backgroundBehavior);

        public static void Restore(this InputSettings settings, in SavedInputSettings savedSettings)
        {
            settings.updateMode = savedSettings.UpdateMode;
            settings.backgroundBehavior = savedSettings.BackgroundBehavior;
        }
    }
}

#endif
