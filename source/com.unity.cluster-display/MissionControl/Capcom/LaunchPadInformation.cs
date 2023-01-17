using System;
using System.Net.Sockets;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// All the information about a launchpad.
    /// </summary>
    public class LaunchPadInformation : IDisposable
    {
        /// <summary>
        /// Definition of the LaunchPad (does not depend on the configuration of the mission).
        /// </summary>
        public MissionControl.LaunchPad Definition
        {
            get => m_Definition;
            set
            {
                if (m_Definition != null && value.Endpoint != m_Definition.Endpoint)
                {
                    ClearCapsuleStream();
                }
                m_Definition = value;
            }
        }

        /// <summary>
        /// Definition of the LaunchComplex owning this LaunchPad (does not depend on configuration of the mission).
        /// </summary>

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public LaunchComplex ComplexDefinition { get; set; }

        /// <summary>
        /// Configuration of the LaunchPad for the mission.
        /// </summary>

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public LaunchPadConfiguration Configuration { get; set; }

        /// <summary>
        /// Configuration of the LaunchComplex owning the LaunchPad for the mission.
        /// </summary>

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public LaunchComplexConfiguration ComplexConfiguration { get; set; }

        /// <summary>
        /// Latest launchpad's status.
        /// </summary>
        public LaunchPadStatus Status { get; set; }

        /// <summary>
        /// ClusterDisplay NodeId assigned to the node executed by this LaunchPad.
        /// </summary>
        public int NodeId { get; set; } = -1;

        /// <summary>
        /// ClusterDisplay role assigned to the node executed by this LaunchPad.
        /// </summary>
        public string NodeRole { get; set; } = LaunchParameterConstants.NodeRoleUnassigned;

        /// <summary>
        /// Port to which the capsule will be listening for request from capcom.
        /// </summary>
        public int CapsulePort
        {
            get => m_CapsulePort;
            set
            {
                if (value != m_CapsulePort)
                {
                    ClearCapsuleStream();
                    m_CapsulePort = value;
                }
            }
        }

        /// <summary>
        /// <see cref="NetworkStream"/> to read or write to the associated capsule (through a TCP connection).
        /// </summary>
        public NetworkStream StreamToCapsule
        {
            get
            {
                // Create the connection
                if (m_CapsuleTcpClient == null)
                {
                    m_CapsuleTcpClient = new TcpClient(m_Definition.Endpoint.Host, CapsulePort);
                    m_CapsuleNetworkStream = m_CapsuleTcpClient.GetStream();

                    ConnectionInit initStruct = new() {MessageFlow = MessageDirection.CapcomToCapsule};
                    m_CapsuleNetworkStream.WriteStruct(initStruct);
                }

                return m_CapsuleNetworkStream;
            }
        }

        public void Dispose()
        {
            ClearCapsuleStream();
        }

        /// <summary>
        /// Clear TcpClient and NetworkStream to the capsule.
        /// </summary>
        void ClearCapsuleStream()
        {
            if (m_CapsuleNetworkStream != null)
            {
                m_CapsuleNetworkStream.Dispose();
                m_CapsuleNetworkStream = null;
            }
            if (m_CapsuleTcpClient != null)
            {
                m_CapsuleTcpClient.Dispose();
                m_CapsuleTcpClient = null;
            }
        }

        /// <summary>
        /// Definition of the LaunchPad (does not depend on the configuration of the mission).
        /// </summary>
        MissionControl.LaunchPad m_Definition;

        /// <summary>
        /// Port to which the capsule will be listening for request from capcom.
        /// </summary>
        int m_CapsulePort = LaunchParameterConstants.DefaultCapsuleBasePort;

        /// <summary>
        /// TCP connection to the capsule
        /// </summary>
        TcpClient m_CapsuleTcpClient;

        /// <summary>
        /// Stream to read or write from m_CapsuleTcpClient.
        /// </summary>
        NetworkStream m_CapsuleNetworkStream;
    }
}
