using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Helpers
{
    [RequireComponent(typeof(ClusterDisplayMouseInputModule))]
    [DefaultExecutionOrder(-1)]
    internal class UIControllerInputReceiver : MonoBehaviour, ControllerInputReceiver.IReceiver
    {
        [SerializeField] private ClusterDisplayMouseInputModule m_InputModule;

        private void OnValidate()
        {
            if (m_InputModule == null)
                m_InputModule = GetComponent<ClusterDisplayMouseInputModule>();
        }

        private void Start()
        {
            if (!ControllerInputReceiver.TryGetInstance(out var controllerInputReceiver))
                return;

            controllerInputReceiver.onMessageReceived -= OnMessageReceived;
            controllerInputReceiver.onMessageReceived += OnMessageReceived;
        }

        public void OnMessageReceived(ControllerInputBase<ControllerInputReceiver>.MessageType messageType, byte[] messageData, int payloadOffset, int payloadSize)
        {
            if (messageType != ControllerInputBase<ControllerInputReceiver>.MessageType.ScreenDimension)
                return;

            var screenDimension = ControllerMessageUtils.BytesToValueType<Vector2>(messageData, payloadOffset);

            if (m_InputModule != null)
                m_InputModule.ControllerSpaceDimension = screenDimension;
            else Debug.LogError($"Missing reference to instance of: \"{nameof(ClusterDisplayMouseInputModule)}\".");
        }
    }
}
