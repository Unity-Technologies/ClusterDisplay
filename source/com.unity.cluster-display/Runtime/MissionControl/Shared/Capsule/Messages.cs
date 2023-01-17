using System;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    public static class MessagesId
    {
        public static readonly Guid Land = Guid.Parse("4CAC1688-72E9-48D4-9B5F-FECA1EDE162E");
        public static readonly Guid CapsuleStatus = Guid.Parse("06E5C6DC-435B-45A9-9836-E2972F5CDD10");
    }

    /// <summary>
    /// Information associated to a Land message (asking the ClusterDisplay application to exit).
    /// </summary>
    public struct LandMessage
    {
    }

    /// <summary>
    /// Response to a <see cref="LandMessage"/>.
    /// </summary>
    public struct LandResponse
    {
    }

    /// <summary>
    /// Message sent from the capsule to capcom to inform of a change in status of the capsule.
    /// </summary>
    public struct CapsuleStatusMessage
    {
        public byte NodeRole;
        public byte NodeId;
        public byte RenderNodeId;
    }

    /// <summary>
    /// Response to a <see cref="CapsuleStatusMessage"/>.
    /// </summary>
    public struct CapsuleStatusResponse
    {

    }
}
