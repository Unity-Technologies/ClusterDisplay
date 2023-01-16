#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Attach this script to a GameObject to enable synchronization of InputSystem events
    /// across all the nodes. Any input events processed by the emitter are replicated on the
    /// repeaters.
    /// Only one instance of this script is necessary in each scene.
    /// </summary>
    /// <remarks>
    /// To ensure perfect synchronization, the player must be executed with the "-delayRepeaters" argument.
    /// This is because the input events arrive on the repeaters one frame after they are processed on the
    /// emitter.
    /// </remarks>
    public class InputSystemReplicator : MonoBehaviour
    {
        const int k_CaptureMemoryDefaultSize = 512;
        const int k_CaptureMemoryMaxSize = 1024;

        readonly InputEventTrace m_EventTrace = new(k_CaptureMemoryDefaultSize, growBuffer: true, k_CaptureMemoryMaxSize);
        InputEventTrace.ReplayController m_ReplayController;
        readonly MemoryStream m_MemoryStream = new(k_CaptureMemoryMaxSize);

        readonly List<InputDevice> m_RealDevices = new();
        readonly List<InputDevice> m_VirtualDevices = new();
        readonly Dictionary<int, int> m_VirtualDeviceMapping = new();

        bool m_UpdatingVirtualDevice;
        SavedInputSettings m_SavedInputSettings;
        bool m_UpdatesControlledExternally;

        void OnEnable()
        {
            m_SavedInputSettings = InputSystem.settings.Save();

            // We want to control when we deserialize and deserialize+apply input events by calling
            // InputSystem.Update() manually.
            m_UpdatesControlledExternally = m_SavedInputSettings.UpdateMode is InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            switch (ClusterDisplayState.GetNodeRole())
            {
                case NodeRole.Emitter:
                    // Emitter records input events and transmits them over the cluster.
                    m_EventTrace.Enable();
                    EmitterStateWriter.RegisterOnStoreCustomDataDelegate((int)StateID.InputSystem, WriteInputData);
                    break;
                case NodeRole.Repeater:
                    // Repeater receives input data from the cluster.
                    RepeaterStateReader.RegisterOnLoadDataDelegate((int)StateID.InputSystem, LoadInputData);
                    // For local testing, the repeater needs the input system to work when it's not in focus.
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    // Repeater cannot accept real inputs, or else the cluster will get out of sync.
                    DisableRealInputs();
                    break;
                case NodeRole.Unassigned:
                    enabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void DisableRealInputs()
        {
            foreach(var device in InputSystem.devices)
            {
                OnDeviceChange(device, InputDeviceChange.Added);
            }

            // Handle devices that get added late. Not all devices are enumerable on startup.
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void EnableRealInputs()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (var device in m_RealDevices)
            {
                Debug.Log($"Enabling {device.name}");
                InputSystem.EnableDevice(device);
            }
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!m_UpdatingVirtualDevice && device != null)
            {
                switch (change)
                {
                    case InputDeviceChange.Added when !m_RealDevices.Contains(device):
                        InputSystem.DisableDevice(device);
                        m_RealDevices.Add(device);
                        ClusterDebug.Log($"Disabled {device.name ?? "unknown"}");
                        break;
                    case InputDeviceChange.Removed when m_RealDevices.Contains(device):
                        m_RealDevices.Remove(device);
                        break;
                }
            }
        }

        void UpdateVirtualDevices()
        {
            foreach (var evt in new InputEventReader(m_EventTrace))
            {
                if (m_VirtualDeviceMapping.ContainsKey(evt.deviceId)) continue;

                if (!m_EventTrace.TryGetDeviceInfo(evt.deviceId, out var deviceInfo)) continue;

                var layoutName = new InternedString(deviceInfo.layout);

                // InputSystem.AddDevice triggers our OnDeviceChanged callback. We don't want to disable our virtual
                // devices.
                m_UpdatingVirtualDevice = true;
                try
                {
                    var device = InputSystem.AddDevice(layoutName);
                    m_VirtualDevices.Add(device);
                    m_VirtualDeviceMapping[evt.deviceId] = device.deviceId;
                }
                finally
                {
                    m_UpdatingVirtualDevice = false;
                }
            }
        }

        void CleanUpVirtualDevices()
        {
            m_UpdatingVirtualDevice = true;
            foreach (var device in m_VirtualDevices)
            {
                InputSystem.RemoveDevice(device);
            }
            m_UpdatingVirtualDevice = false;
            m_VirtualDevices.Clear();
            m_VirtualDeviceMapping.Clear();
        }

        bool LoadInputData(NativeArray<byte> stateData)
        {
            // There's no API to copy unmanaged bytes directly into InputEventTrace, so we'll take a roundabout
            // approach using MemoryStream.
            // First, copy the bytes into a MemoryStream.
            m_MemoryStream.Write(stateData.AsReadOnlySpan());
            m_MemoryStream.Flush();

            // Next, reset the stream so we can read from the beginning.
            m_MemoryStream.Position = 0;
            // FIXME: ReadFrom() is not alloc-free.
            m_EventTrace.ReadFrom(m_MemoryStream);

            // Clear the stream for re-use.
            m_MemoryStream.SetLength(0);

            ClusterDebug.Log($"Received input data {m_EventTrace.totalEventSizeInBytes} bytes");

            if (m_ReplayController == null)
            {
                // Create a new ReplayController associated with our EventTrace instance
                m_ReplayController = m_EventTrace.Replay();
            }
            else
            {
                // The event trace data has been updated.
                // Set the read head back to the beginning.
                m_ReplayController.Rewind();
            }

            // Play back the events remapped to virtual devices.
            UpdateVirtualDevices();
            foreach (var (from, to) in m_VirtualDeviceMapping)
            {
                m_ReplayController.WithDeviceMappedFromTo(from, to);
            }
            m_ReplayController.PlayAllEvents();
            return true;
        }

        int WriteInputData(NativeArray<byte> writeableBuffer)
        {
            if (writeableBuffer.Length < m_MemoryStream.Length)
            {
                // Ask for a larger buffer
                return -1;
            }

            // Copy the memory stream contents (the serialized event data) into the FrameData to be transmitted
            // over the cluster network.
            int bytesRead = m_MemoryStream.Read(writeableBuffer.AsSpan());
            return bytesRead;
        }

        void OnDisable()
        {
            m_EventTrace.Disable();
            m_ReplayController?.Dispose();
            m_ReplayController = null;
            RepeaterStateReader.UnregisterOnLoadDataDelegate((int)StateID.InputSystem, LoadInputData);
            EmitterStateWriter.UnregisterCustomDataDelegate((int)StateID.InputSystem, WriteInputData);
            EnableRealInputs();
            CleanUpVirtualDevices();
            InputSystem.settings.Restore(m_SavedInputSettings);
        }

        void OnDestroy()
        {
            m_EventTrace.Dispose();
        }

        void Update()
        {
            if (!m_UpdatesControlledExternally)
                InputSystem.Update();
            if (ClusterDisplayState.GetNodeRole() == NodeRole.Emitter)
            {
                // Serialize the event trace data, which will get copied into the FrameData and transmitted
                // at the next sync point.
                m_MemoryStream.SetLength(0);
                m_EventTrace.WriteTo(m_MemoryStream);
                m_MemoryStream.Flush();
                m_MemoryStream.Position = 0;
            }

            m_EventTrace.Clear();
        }
    }
}
#endif
