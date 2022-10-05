using System.Diagnostics;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    /// <summary>
    /// Base class for service exposing a single <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="ObservableObject"/> we are exposing.</typeparam>
    public class ObservableObjectService<T>: ObservableObjectServiceBase where T: ObservableObject
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serviceProvider">Services provider.</param>
        /// <param name="observableObject">The <see cref="ObservableObject"/> we are serving.</param>
        /// <param name="name">Name of the <see cref="ObservableObjectService{T}"/> (should be unique among all
        /// registered <see cref="ObservableObjectService{T}"/>s).</param>
        protected ObservableObjectService(IServiceProvider serviceProvider, T observableObject, string name): base(name)
        {
            var applicationLifetime = serviceProvider.GetService<IHostApplicationLifetime>()!;
            m_ObservableObject = observableObject;
            m_ObservableObject.OnObjectChanged += OnObjectChanged;

            serviceProvider.GetService<ObservableObjectCatalogService>()!.AddObservableObject(this);

            applicationLifetime.ApplicationStopping.Register(ApplicationShutdown);
        }

        /// <summary>
        /// Lock the <typeparamref name="T"/> so that caller can access it safely.
        /// </summary>
        /// <returns><see cref="AsyncLockedObject{T}"/> through which the caller can access the
        /// <typeparamref name="T"/>.  Caller should dispose of the returned value as soon as he's done of it to unlock
        /// the <typeparamref name="T"/> (so that other threads can access it).</returns>
        public async Task<AsyncLockedObject<T>> LockAsync()
        {
            return new AsyncLockedObject<T>(m_ObservableObject, await m_Lock.LockAsync());
        }

        /// <summary>
        /// Returns a task that will provide the update of the object to be sent over REST.
        /// </summary>
        /// <param name="minVersionNumber">Minimum value of <see cref="VersionNumber"/> to return a value.</param>
        /// <param name="cancellationToken">Token that when signal cancels the returned <see cref="Task"/>.</param>
        public override async Task<ObservableObjectUpdate> GetValueFromVersionAsync(ulong minVersionNumber,
            CancellationToken cancellationToken)
        {
            for (; ; )
            {
                cancellationToken.ThrowIfCancellationRequested();

                Task somethingChangedTask;
                using (await m_Lock.LockAsync())
                {
                    if (m_VersionNumber >= minVersionNumber)
                    {
                        ObservableObjectUpdate ret = new();
                        ret.Updated = JsonSerializer.SerializeToElement(m_ObservableObject, Json.SerializerOptions);
                        ret.NextUpdate = m_VersionNumber + 1;
                        return ret;
                    }

                    m_ChangedTaskCompletionSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    somethingChangedTask = m_ChangedTaskCompletionSource.Task;
                }

                await somethingChangedTask.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Current version number of the
        /// </summary>
        public ulong VersionNumber
        {
            get
            {
                using (m_Lock.Lock())
                {
                    return m_VersionNumber;
                }
            }
        }

        /// <summary>
        /// Method called when the application is requested to shutdown.
        /// </summary>
        void ApplicationShutdown()
        {
            using (m_Lock.Lock())
            {
                m_ChangedTaskCompletionSource?.TrySetCanceled();
            }
        }

        /// <summary>
        /// Method called when m_ObservableObject is modified.
        /// </summary>
        /// <param name="obj"><see cref="m_ObservableObject"/>.</param>
        void OnObjectChanged(ObservableObject obj)
        {
            Debug.Assert(obj == m_ObservableObject);
            // Should be locked since we are being modified, otherwise someone is modifying the object without the
            // lock...
            Debug.Assert(m_Lock.IsLocked);

            ++m_VersionNumber;

            m_ChangedTaskCompletionSource?.TrySetResult();
            m_ChangedTaskCompletionSource = null;
        }

        /// <summary>
        /// Synchronize access to member variables
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// The <see cref="ObservableObject"/> we are serving.
        /// </summary>
        readonly T m_ObservableObject;

        /// <summary>
        /// Version number of the <see cref="ObservableObject"/> we are serving.
        /// </summary>
        ulong m_VersionNumber;

        /// <summary>
        /// <see cref="TaskCompletionSource"/> that get triggered every time <see cref="m_ObservableObject"/> changes.
        /// </summary>
        TaskCompletionSource? m_ChangedTaskCompletionSource;
    }
}
