using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Contains various constant data used to deal with launch parameters.
    /// </summary>
    public static class LaunchParameterConstants
    {
        public const string NodeIdParameterId = "NodeId";
        public const string NodeRoleParameterId = "NodeRole";
        public const string RepeaterCountParameterId = "RepeaterCount";
        public const string BackupNodeCountParameterId = "BackupNodeCount";
        public const string MulticastAddressParameterId = "MulticastAddress";
        public const string MulticastPortParameterId = "MulticastPort";
        public const string MulticastAdapterNameParameterId = "MulticastAdapterName";
        public const string TargetFrameRateParameterId = "TargetFrameRate";
        public const string DelayRepeatersParameterId = "DelayRepeaters";
        public const string HeadlessEmitterParameterId = "HeadlessEmitter";
        public const string ReplaceHeadlessEmitterParameterId = "ReplaceHeadlessEmitter";
        public const string HandshakeTimeoutParameterId = "HandshakeTimeout";
        public const string CommunicationTimeoutParameterId = "CommunicationTimeout";
        public const string EnableHardwareSyncParameterId = "EnableHardwareSync";
        public const string CapsuleBasePortParameterId = "CapsuleBasePort";
        public const string DeleteRegistryKeyParameterId = "DeleteRegistryKey";
        public const string ShowTestPattern = "ShowTestPattern";

        public const string NodeRoleUnassigned = "Unassigned";
        public const string NodeRoleEmitter = "Emitter";
        public const string NodeRoleRepeater = "Repeater";
        public const string NodeRoleBackup = "Backup";

        public const int DefaultCapsuleBasePort = 8300;
        public const bool DefaultDeleteRegistryKey = false;
    }
}
