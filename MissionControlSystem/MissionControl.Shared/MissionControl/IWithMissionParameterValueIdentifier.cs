using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Interface exposed by classes related to mission parameter that have a ValueIdentifier property.
    /// </summary>
    public interface IWithMissionParameterValueIdentifier
    {
        string ValueIdentifier { get; set; }
    }
}
