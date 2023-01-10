namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Interface to be exposed by different processes of <see cref="Application"/> that want to be called every time
    /// there might be something new to do.
    /// </summary>
    public interface IApplicationProcess
    {
        /// <summary>
        /// There might be something new to do, execute the process.
        /// </summary>
        /// <param name="missionControlMirror">Data mirrored from MissionControl.</param>
        void Process(MissionControlMirror missionControlMirror);
    }
}
