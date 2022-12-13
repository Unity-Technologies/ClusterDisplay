namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// <see cref="IApplicationProcess"/> responsible for updating <see cref="LaunchPadInformation"/>'s status.
    /// </summary>
    public class UpdateLaunchPadStatusProcess: IApplicationProcess
    {
        public void Process(MissionControlMirror missionControlMirror)
        {
            if (missionControlMirror.LaunchPadsStatusNextVersion == m_LastStatusNextVersion)
            {
                return;
            }

            m_LastStatusNextVersion = missionControlMirror.LaunchPadsStatusNextVersion;

            foreach (var launchPadInformation in missionControlMirror.LaunchPadsInformation)
            {
                missionControlMirror.LaunchPadsStatus.TryGetValue(launchPadInformation.Definition.Identifier,
                    out var status);
                if (status is {IsDefined: false})
                {
                    status = null;
                }
                launchPadInformation.Status = status;
            }
        }

        ulong m_LastStatusNextVersion = ulong.MaxValue;
    }
}
