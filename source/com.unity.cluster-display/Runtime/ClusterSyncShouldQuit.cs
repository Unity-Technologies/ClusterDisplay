namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Interface for service to be added to <see cref="Utils.ServiceLocator"/> to indicate that the cluster display
    /// node application should quit.
    /// </summary>
    /// <remarks>The mere presence of the service indicate that the cluster display node application should quit, so
    /// it should only be added to <see cref="Utils.ServiceLocator"/> when quitting is requested.
    /// <br/><br/>There is no properties or method to the interface yet as none or needed, but that would be where we
    /// would add additional information to customize quitting if ever it needs to.</remarks>
    public interface IClusterSyncShouldQuit
    {
    }
}
