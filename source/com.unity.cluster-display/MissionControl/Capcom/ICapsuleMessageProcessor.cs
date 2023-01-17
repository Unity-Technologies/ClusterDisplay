using System;
using System.IO;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Interface to be exposed by classes processing messages received from capsules.
    /// </summary>
    public interface ICapsuleMessageProcessor
    {
        /// <summary>
        /// Process the specified message.
        /// </summary>
        /// <param name="mirror">Information about MissionControl capcom is working for.</param>
        /// <param name="launchpadId">Identifier of the Launchpad that launched the capsule.</param>
        /// <param name="stream">Stream from which to read the message and on which to send the answer.</param>
        void Process(MissionControlMirror mirror, Guid launchpadId, Stream stream);
    }
}
