using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Various information to be made accessible to capcom processes working for MissionControl.
    /// </summary>
    public class CapcomUplink: IEquatable<CapcomUplink>
    {
        /// <summary>
        /// Should capcom be running?
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Should the payload proceed with landing (graceful shutdown)?
        /// </summary>
        public bool ProceedWithLanding { get; set; }

        /// <summary>
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void DeepCopyFrom(CapcomUplink from)
        {
            IsRunning = from.IsRunning;
            ProceedWithLanding = from.ProceedWithLanding;
        }

        public bool Equals(CapcomUplink other)
        {
            return other != null &&
                IsRunning == other.IsRunning &&
                ProceedWithLanding == other.ProceedWithLanding;
        }
    }
}
