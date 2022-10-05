namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    /// <summary>
    /// Base class to allow discovering all registered <see cref="ObservableObjectService{T}"/>s and dealing with them
    /// without knowing the generic parameter type.
    /// </summary>
    public abstract class ObservableObjectServiceBase
    {
        protected ObservableObjectServiceBase(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Name of the <see cref="ObservableObjectService{T}"/> (should be unique among all registered
        /// <see cref="ObservableObjectService{T}"/>s).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns a task that will provide the update of the object to be sent over REST.
        /// </summary>
        /// <param name="minVersionNumber">Minimum value of <see cref="VersionNumber"/> to return a value.</param>
        /// <param name="cancellationToken">Token that when signal cancels the returned <see cref="Task"/>.</param>
        public abstract Task<ObservableObjectUpdate> GetValueFromVersionAsync(ulong minVersionNumber,
            CancellationToken cancellationToken);
    }
}
