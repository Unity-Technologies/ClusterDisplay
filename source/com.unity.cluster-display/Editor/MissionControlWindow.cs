using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.ClusterDisplay.MissionControl;

namespace Unity.ClusterDisplay.Editor
{
    class MissionControlWindow : EditorWindow
    {
        UdpClient m_UdpClient = new UdpClient();
        Task m_ListenTask;
        CancellationTokenSource m_CancellationTokenSource = new();

        public MissionControlWindow()
        {
            m_UdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 11000));
            // m_ListenTask = await Listen(m_CancellationTokenSource.Token);
        }
        
        [MenuItem("Cluster Display/Mission Control")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissionControlWindow>();
        }

        void OnDestroy()
        {
            m_CancellationTokenSource.Cancel();
        }

        void OnEnable()
        {
            throw new NotImplementedException();
        }

        void OnGUI()
        {
            EditorGUIUtility.wideMode = true;
            if (GUILayout.Button("Refresh"))
            {
                var sent = m_UdpClient.Send(new byte[1024], 1024, new IPEndPoint(IPAddress.Broadcast, 9876));
                Debug.Log(sent.ToString());
            }
        }

        async IAsyncEnumerable<NodeInfo> Listen([EnumeratorCancellation] CancellationToken token)
        {
            while (true)
            {
                var result = await m_UdpClient.ReceiveAsync().WithCancellation(token);
                yield return result.Buffer.ReadStruct<NodeInfo>();
                Debug.Log($"Response from {result.RemoteEndPoint.Address}");
            }
        }
    }
}
