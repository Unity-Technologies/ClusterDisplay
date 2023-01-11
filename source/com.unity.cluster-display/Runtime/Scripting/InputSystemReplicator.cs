#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
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
        bool m_CreatingVirtualDevice;

        void OnEnable()
        {
            Cursor.visible = true;
            switch (ClusterDisplayState.GetNodeRole())
            {
                case NodeRole.Emitter:
                    m_EventTrace.Enable();
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                    EmitterStateWriter.RegisterOnStoreCustomDataDelegate((int)StateID.InputSystem, OnStoreInputData);
                    break;
                case NodeRole.Repeater:
                    RepeaterStateReader.RegisterOnLoadDataDelegate((int)StateID.InputSystem, OnLoadInputData);
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
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

            // Handle devices that get added late.
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void EnableRealInputs()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (var device in m_RealDevices)
            {
                InputSystem.EnableDevice(device);
            }
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!m_CreatingVirtualDevice &&
                change is InputDeviceChange.Added &&
                device != null &&
                !m_RealDevices.Contains(device))
            {
                InputSystem.DisableDevice(device);
                m_RealDevices.Add(device);
                Debug.Log($"Disabled {device.name ?? "unknown"}");
            }
            else if (change is InputDeviceChange.Removed)
            {
                m_RealDevices.Remove(device);
            }
        }

        void UpdateVirtualDevices()
        {
            foreach (var evt in m_EventTrace)
            {
                if (m_VirtualDeviceMapping.ContainsKey(evt.deviceId)) continue;

                var deviceIndex = m_EventTrace.deviceInfos.IndexOf(info => info.deviceId == evt.deviceId);
                if (deviceIndex < 0) continue;

                var deviceInfo = m_EventTrace.deviceInfos[deviceIndex];
                var layoutName = new InternedString(deviceInfo.layout);

                // Create device.
                m_CreatingVirtualDevice = true;
                var device = InputSystem.AddDevice(layoutName);
                m_CreatingVirtualDevice = false;
                m_VirtualDevices.Add(device);
                m_VirtualDeviceMapping[evt.deviceId] = device.deviceId;
            }
        }

        void CleanUpVirtualDevices()
        {
            foreach (var device in m_VirtualDevices)
            {
                InputSystem.RemoveDevice(device);
            }
            m_VirtualDevices.Clear();
            m_VirtualDeviceMapping.Clear();
        }

        bool OnLoadInputData(NativeArray<byte> stateData)
        {
            m_MemoryStream.Write(stateData.AsReadOnlySpan());
            m_MemoryStream.Flush();
            m_MemoryStream.Position = 0;
            m_EventTrace.ReadFrom(m_MemoryStream);
            Debug.Log($"Received input data {m_EventTrace.totalEventSizeInBytes} bytes");

            m_ReplayController?.Dispose();
            m_ReplayController = m_EventTrace.Replay();

            UpdateVirtualDevices();
            foreach (var (from, to) in m_VirtualDeviceMapping)
            {
                m_ReplayController = m_ReplayController.WithDeviceMappedFromTo(from, to);
            }
            m_ReplayController.PlayAllEvents();

            m_MemoryStream.SetLength(0);
            return true;
        }

        int OnStoreInputData(NativeArray<byte> writeableBuffer)
        {
            if (writeableBuffer.Length < m_MemoryStream.Length)
            {
                // Ask for a larger buffer
                return -1;
            }

            int bytesRead = m_MemoryStream.Read(writeableBuffer.AsSpan());
            return bytesRead;
        }

        void OnDisable()
        {
            m_EventTrace.Disable();
            m_ReplayController?.Dispose();
            RepeaterStateReader.UnregisterOnLoadDataDelegate((int)StateID.InputSystem, OnLoadInputData);
            EmitterStateWriter.UnregisterCustomDataDelegate((int)StateID.InputSystem, OnStoreInputData);
            EnableRealInputs();
            CleanUpVirtualDevices();
        }

        void OnDestroy()
        {
            m_EventTrace.Dispose();
        }

        void Update()
        {
            switch (ClusterDisplayState.GetNodeRole())
            {
                case NodeRole.Unassigned:
                    break;
                case NodeRole.Emitter:
                    InputSystem.Update();
                    m_MemoryStream.SetLength(0);
                    m_EventTrace.WriteTo(m_MemoryStream);
                    m_MemoryStream.Flush();
                    m_MemoryStream.Position = 0;
                    m_EventTrace.Clear();
                    break;
                case NodeRole.Repeater:
                    InputSystem.Update();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            m_EventTrace.Clear();
        }
    }
}
#endif
