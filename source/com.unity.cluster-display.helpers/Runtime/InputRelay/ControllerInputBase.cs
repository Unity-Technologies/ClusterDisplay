using System.Runtime.InteropServices;
using LiteNetLib;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.ClusterDisplay.Helpers
{
    internal abstract class ControllerInputBase<T> : SingletonMonoBehaviour<T> where T : ControllerInputBase<T>
    {
        protected NetManager m_NetManager;

        protected const int k_InputBufferMaxSize = 8192;
        protected NativeArray<byte> m_InputBuffer;

        [SerializeField] protected ConnectionSettings m_ConnectionSettings;

        protected const string k_ControllerReceiverArgument = "-controllerReceiver";
        protected const string k_ControllerSenderArgument = "-controllerSender";

        public enum MessageType : byte
        {
            ScreenDimension = 0,
            InputData = 1,
        }

        protected abstract void Connect();
        protected abstract void CleanUp();
        private void Start ()
        {
            if (ClusterDisplayState.IsRepeater)
                return;

            if (Application.isEditor && Application.isPlaying)
                goto connect;

            if (!CommandLineParser.TryParseAddressAndPort(k_ControllerReceiverArgument, out m_ConnectionSettings.emitterAddress, out m_ConnectionSettings.port) &&
                !CommandLineParser.TryParseAddressAndPort(k_ControllerSenderArgument, out m_ConnectionSettings.emitterAddress, out m_ConnectionSettings.port))
                return;

            connect:
            Connect();
        }

        private void OnApplicationQuit() => CleanUp();
        private void OnDestroy() => CleanUp();
    }
}
