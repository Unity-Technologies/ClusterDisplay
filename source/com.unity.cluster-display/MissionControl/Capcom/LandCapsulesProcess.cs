using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.ClusterDisplay.MissionControl.Capsule;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> responsible for sending the landing signal to capsules.
    /// </summary>
    public class LandCapsulesProcess: IApplicationProcess
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LandCapsulesProcess()
        {
            int sizeOfGuid = Marshal.SizeOf<Guid>();
            int sizeOfLandMessage = Marshal.SizeOf<LandMessage>();
            m_LandMessageBuffer = new byte[sizeOfGuid + sizeOfLandMessage];
            var guidToWrite = MessagesId.Land;
            MemoryMarshal.Write(m_LandMessageBuffer.AsSpan(0, sizeOfGuid), ref guidToWrite);
            LandMessage landMessageToWrite = new();
            MemoryMarshal.Write(m_LandMessageBuffer.AsSpan(sizeOfGuid, sizeOfLandMessage),
                ref landMessageToWrite);
        }

        /// <inheritdoc/>
        public void Process(MissionControlMirror missionControlMirror)
        {
            // As anything that interest us changed?
            if (missionControlMirror.CapcomUplinkNextVersion == m_LastCapcomUplinkNextVersion)
            {
                return;
            }
            m_LastCapcomUplinkNextVersion = missionControlMirror.CapcomUplinkNextVersion;

            if (missionControlMirror.CapcomUplink.ProceedWithLanding)
            {
                if (m_LandingSignalSent)
                {
                    return;
                }

                List<Task> sendLandingSignalTasks = new();
                foreach (var launchpad in missionControlMirror.LaunchPadsInformation)
                {
                    sendLandingSignalTasks.Add(LandCapsuleAsync(launchpad));
                }

                Task.WaitAll(sendLandingSignalTasks.ToArray());
            }
            else
            {
                m_LandingSignalSent = false;
            }
        }

        /// <summary>
        /// Send message to land the capsule of the given launchpad.
        /// </summary>
        /// <param name="launchpad">Information about the launchpad.</param>
        async Task LandCapsuleAsync(LaunchPadInformation launchpad)
        {
            await launchpad.StreamToCapsule.WriteAsync(m_LandMessageBuffer).ConfigureAwait(false);

            // Read the response (to be a good citizen and keep the socket clean, because we are not really using it).
            byte[] responseBytes = new byte[Marshal.SizeOf<LandResponse>()];
            await launchpad.StreamToCapsule.ReadStructAsync<LandResponse>(responseBytes).ConfigureAwait(false);
        }

        /// <summary>
        /// Last version of <see cref="MissionControlMirror.CapcomUplinkNextVersion"/> that the
        /// <see cref="Process"/> method was called on.
        /// </summary>
        ulong m_LastCapcomUplinkNextVersion = ulong.MaxValue;
        /// <summary>
        /// Was the landing signal sent to the capsules?
        /// </summary>
        bool m_LandingSignalSent;
        /// <summary>
        /// Message to send to request landing of a capsule
        /// </summary>
        byte[] m_LandMessageBuffer;
    }
}
