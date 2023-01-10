using System;

namespace Unity.ClusterDisplay.MissionControl.Capsule
{
    public static class MessagesId
    {
        public static readonly Guid Land = Guid.Parse("4CAC1688-72E9-48D4-9B5F-FECA1EDE162E");
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
}
