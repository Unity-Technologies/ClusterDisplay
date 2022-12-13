using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.ClusterDisplay.MissionControl.Capsule;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Capcom application logic.
    /// </summary>
    public class Application
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="missionControlEntry">Base Uri to use to access MissionControl.</param>
        public Application(Uri missionControlEntry)
        {
            m_HttpClient.BaseAddress = missionControlEntry;
            // Blocking calls from mission control should return after +/- 3 minutes, so setting the timeout to 10
            // minutes should be safe enough.
            m_HttpClient.Timeout = TimeSpan.FromMinutes(10);

            m_MissionControlMirror = new(m_HttpClient);

            m_ObjectsToMirror = new()
            {
                new ToMirrorObject() { Name ="status",
                    GetNextVersion = () => m_MissionControlMirror.StatusNextVersion,
                    UpdateCallback = UpdateStatus },
                new ToMirrorObject() { Name ="capcomUplink",
                    GetNextVersion = () => m_MissionControlMirror.CapcomUplinkNextVersion,
                    UpdateCallback = UpdateCapcomUplink },
                new ToMirrorObject() { Name ="currentMission/launchConfiguration",
                    GetNextVersion = () => m_MissionControlMirror.LaunchConfigurationNextVersion,
                    UpdateCallback = UpdateLaunchConfiguration }
            };
            m_CollectionsToMirror = new()
            {
                new ToMirrorCollection<MissionControl.LaunchComplex>() { Name ="complexes",
                    GetNextVersion = () => m_MissionControlMirror.ComplexesNextVersion,
                    UpdateCallback = UpdateLaunchComplexes },
                new ToMirrorCollection<MissionControl.LaunchParameterForReview>() {
                    Name ="currentMission/launchParametersForReview",
                    GetNextVersion = () => m_MissionControlMirror.LaunchParametersForReviewNextVersion,
                    UpdateCallback = UpdateLaunchParametersForReview },
                new ToMirrorCollection<MissionControl.Asset>() {
                    Name ="assets",
                    GetNextVersion = () => m_MissionControlMirror.AssetsNextVersion,
                    UpdateCallback = UpdateAssets },
                new ToMirrorCollection<MissionControl.LaunchPadStatus>() {
                    Name ="launchPadsStatus",
                    GetNextVersion = () => m_MissionControlMirror.LaunchPadsStatusNextVersion,
                    UpdateCallback = UpdateLaunchPadsStatus }
            };
            m_Processes = new()
            {
                new ShutdownCapcomProcess(m_StopApplication),
                new ReviewLaunchParametersProcess(new (){BaseAddress = missionControlEntry}),
                new UpdateLaunchPadStatusProcess(),
                new MonitorCapsulesProcess(m_StopApplication.Token, this),
                new LandCapsulesProcess()
            };
            m_CapsuleMessageProcessors.Add(MessagesId.CapsuleStatus, new CapsuleStatusProcessor());

            m_StopApplication.Token.Register(() => m_StartProcessingLoop.Cancel());
        }

        /// <summary>
        /// Adds a new process to the application (that is not owned by the application).
        /// </summary>
        /// <param name="newProcess">The process to add</param>
        public void AddProcess(IApplicationProcess newProcess)
        {
            lock (m_Lock)
            {
                if (m_Started)
                {
                    throw new InvalidOperationException("Application already started.");
                }
            }
            m_Processes.Add(newProcess);
        }

        /// <summary>
        /// Start the application loop.
        /// </summary>
        /// <remarks>The return task will complete when the application has been asked to stop and it ready to exit.
        /// </remarks>
        public async Task Start()
        {
            lock (m_Lock)
            {
                if (m_Started)
                {
                    throw new InvalidOperationException("Application already started.");
                }

                m_Started = true;
            }

            try
            {
                await Task.WhenAll(MonitorChanges("api/v1/objectsUpdate", m_ObjectsToMirror),
                    MonitorChanges("api/v1/incrementalCollectionsUpdate", m_CollectionsToMirror),
                    ProcessingLoop());
            }
            catch (Exception)
            {
                if (!m_StopApplication.IsCancellationRequested)
                {
                    // Not normal, bubble up exception
                    throw;
                }
                // else this is normal, no need to report the exception
            }
        }

        /// <summary>
        /// Force stopping the application.
        /// </summary>
        /// <remarks>Normally there is no need to call this method as we monitor mission control.  This is to make our
        /// life easier in some unit tests.</remarks>
        public void ManualStop()
        {
            m_StopApplication.Cancel();
        }

        /// <summary>
        /// Queue a message received from a capsule to be processed.
        /// </summary>
        /// <param name="launchPadId">Identifier of the Launchpad that launched the capsule.</param>
        /// <param name="messageId">Received message identifier.</param>
        /// <param name="networkStream">Stream from which to read the message and on which to send the answer.</param>
        /// <param name="postProcess">To be executed once we are done processing the message.</param>
        public void QueueMessageFromCapsule(Guid launchPadId, Guid messageId, NetworkStream networkStream,
            Action postProcess)
        {
            lock (m_Lock)
            {
                m_MessagesFromCapsules.Add(new()
                {
                    LaunchPadId = launchPadId,
                    MessageId = messageId,
                    NetworkStream = networkStream,
                    PostProcess = postProcess
                });
                m_StartProcessingLoop.Signal();
            }
        }

        /// <summary>
        /// Loop that monitor MissionControl objects exposed by the objectsUpdate method.
        /// </summary>
        /// <param name="baseUrl">The address to monitor for changes.</param>
        /// <param name="toMirrorList">List of things to monitor for changes.</param>
        async Task MonitorChanges(string baseUrl, List<ToMirror> toMirrorList)
        {
            while (!m_StopApplication.IsCancellationRequested)
            {
                // Get next set of updates
                StringBuilder locatorBuilder = new(baseUrl);
                for (int toMirrorIndex = 0; toMirrorIndex < toMirrorList.Count; ++toMirrorIndex)
                {
                    var toMirror = toMirrorList[toMirrorIndex];
                    locatorBuilder.Append(toMirrorIndex == 0 ? '?' : '&');
                    locatorBuilder.AppendFormat("name{0}={1}&fromVersion{0}={2}", toMirrorIndex, toMirror.Name,
                        toMirror.GetNextVersion());
                }

                var responseMessage = await m_HttpClient.GetAsync(locatorBuilder.ToString(), m_StopApplication.Token)
                    .ConfigureAwait(false);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    // TODO: Log
                    await Task.Delay(TimeSpan.FromMilliseconds(250), m_StopApplication.Token).ConfigureAwait(false);
                    continue;
                }
                if (responseMessage.StatusCode == HttpStatusCode.NoContent)
                {   // This will happen if there is no content for some time, just ask again
                    continue;
                }

                // Process received updates
                // Remarks: We keep m_Lock locked for the "whole time" we are processing received update (as opposed to
                // locking it only for the actual set operation) as we want to be suer IApplicationProcess are not
                // called with partial content of the received update.
                var updateJson = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject update;
                try
                {
                    update = JObject.Parse(updateJson);
                }
                catch (Exception)
                {
                    // This shouldn't happen, however we can easily recover by trying again and hopping to receive a
                    // valid json this time.  Wait a little bit before retrying to avoid hammering the system.
                    // TODO: Log
                    await Task.Delay(TimeSpan.FromMilliseconds(250), m_StopApplication.Token).ConfigureAwait(false);
                    continue;
                }

                lock (m_Lock)
                {
                    foreach (var toMirror in toMirrorList)
                    {
                        try
                        {
                            if (update.TryGetValue(toMirror.Name, out var objectUpdateToken) &&
                                objectUpdateToken is JObject objectUpdate)
                            {
                                if (toMirror.ProcessChanges(objectUpdate))
                                {
                                    m_StartProcessingLoop.Signal();
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // TODO: Log
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Main loop of the application.
        /// </summary>
        async Task ProcessingLoop()
        {
            while (!m_StopApplication.IsCancellationRequested)
            {
                Task waitTask;
                lock (m_Lock)
                {
                    // First apply the various processes reacting to MissionControl changes
                    foreach (var process in m_Processes)
                    {
                        try
                        {
                            process.Process(m_MissionControlMirror);
                        }
                        catch (Exception)
                        {
                            // TODO: log
                        }
                    }

                    // Then check for capsule messages to process (done after so that it can benefit from changes made
                    // to m_MissionControlMirror by the IApplicationProcesses).
                    foreach (var message in m_MessagesFromCapsules)
                    {
                        if (m_CapsuleMessageProcessors.TryGetValue(message.MessageId, out var processor))
                        {
                            try
                            {
                                processor.Process(m_MissionControlMirror, message.LaunchPadId, message.NetworkStream);
                                message.PostProcess();
                            }
                            catch (Exception)
                            {
                                // TODO: log
                            }
                        }
                        else
                        {
                            // TODO: log
                        }
                    }
                    m_MessagesFromCapsules.Clear();

                    waitTask = m_StartProcessingLoop.SignaledTask;
                }

                await Task.WhenAny(waitTask).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's Status.
        /// </summary>
        /// <param name="newValueJson">New value (json)</param>
        /// <param name="nextVersion">Next version to ask for.</param>
        void UpdateStatus(JObject newValueJson, ulong nextVersion)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.Status = newValueJson.ToObject<MissionControl.Status>(Json.Serializer);
            m_MissionControlMirror.StatusNextVersion = nextVersion;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's CapcomUplink.
        /// </summary>
        /// <param name="newValueJson">New value (json)</param>
        /// <param name="nextVersion">Next version to ask for.</param>
        void UpdateCapcomUplink(JObject newValueJson, ulong nextVersion)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.CapcomUplink =
                newValueJson.ToObject<MissionControl.CapcomUplink>(Json.Serializer);
            m_MissionControlMirror.CapcomUplinkNextVersion = nextVersion;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's LaunchConfiguration.
        /// </summary>
        /// <param name="newValueJson">New value (json)</param>
        /// <param name="nextVersion">Next version to ask for.</param>
        void UpdateLaunchConfiguration(JObject newValueJson, ulong nextVersion)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.LaunchConfiguration =
                newValueJson.ToObject<MissionControl.LaunchConfiguration>(Json.Serializer);
            m_MissionControlMirror.LaunchConfigurationNextVersion = nextVersion;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's LaunchComplex collection.
        /// </summary>
        /// <param name="update">New value (json)</param>
        void UpdateLaunchComplexes(IncrementalCollectionUpdate<MissionControl.LaunchComplex> update)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.Complexes.ApplyDelta(update);
            m_MissionControlMirror.ComplexesNextVersion = update.NextUpdate;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's LaunchParameterForReview
        /// collection.
        /// </summary>
        /// <param name="update">New value (json)</param>
        void UpdateLaunchParametersForReview(IncrementalCollectionUpdate<MissionControl.LaunchParameterForReview> update)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.LaunchParametersForReview.ApplyDelta(update);
            m_MissionControlMirror.LaunchParametersForReviewNextVersion = update.NextUpdate;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's Asset collection.
        /// </summary>
        /// <param name="update">New value (json)</param>
        void UpdateAssets(IncrementalCollectionUpdate<MissionControl.Asset> update)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.Assets.ApplyDelta(update);
            m_MissionControlMirror.AssetsNextVersion = update.NextUpdate;
        }

        /// <summary>
        /// Update <see cref="m_MissionControlMirror"/> from changes in MissionControl's LaunchPadStatus collection.
        /// </summary>
        /// <param name="update">New value (json)</param>
        void UpdateLaunchPadsStatus(IncrementalCollectionUpdate<MissionControl.LaunchPadStatus> update)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));
            m_MissionControlMirror.LaunchPadsStatus.ApplyDelta(update);
            m_MissionControlMirror.LaunchPadsStatusNextVersion = update.NextUpdate;
        }

        /// <summary>
        /// Stores information on how to deal with each object or collection we update from MissionControl.
        /// </summary>
        abstract class ToMirror
        {
            /// <summary>
            /// Name of the object or collection in MissionControl.
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// Callback to call to get the next version we are interested in.
            /// </summary>
            public Func<ulong> GetNextVersion { get; set; }
            /// <summary>
            /// Perform the update from the json changes received.
            /// </summary>
            /// <param name="changesJson">Json changes.</param>
            /// <returns>Was the operation successful?</returns>
            /// <remarks>Caller has already locked <see cref="Application.m_Lock"/> and so
            /// <see cref="m_MissionControlMirror"/> can be changed.</remarks>
            public abstract bool ProcessChanges(JObject changesJson);
        };

        /// <summary>
        /// Stores information on how to deal with each object we update from MissionControl.
        /// </summary>
        class ToMirrorObject : ToMirror
        {
            /// <inheritdoc/>
            public override bool ProcessChanges(JObject changesJson)
            {
                ulong nextVersion;
                if (changesJson.TryGetValue("nextUpdate", out var newFromVersion) &&
                    newFromVersion is JValue {Type: JTokenType.Integer} newFromVersionValue)
                {
                    nextVersion = newFromVersionValue.Value<ulong>();
                }
                else
                {
                    // TODO: Log
                    return false;
                }

                if (changesJson.TryGetValue("updated", out var updateValue) &&
                    updateValue is JObject updateValueObject)
                {
                    UpdateCallback(updateValueObject, nextVersion);
                    return true;
                }
                else
                {
                    // TODO: Log
                    return false;
                }
            }

            /// <summary>
            /// Callback executed to update our mirror data from the json representation of MissionControl.
            /// </summary>
            /// <remarks><see cref="Application.m_Lock"/> is already locked when called and so
            /// <see cref="m_MissionControlMirror"/> can be changed.</remarks>
            public Action<JObject, ulong> UpdateCallback { get; set; }
        }

        /// <summary>
        /// Stores information on how to deal with each <see cref="IncrementalCollection{T}"/> we update from
        /// MissionControl.
        /// </summary>
        class ToMirrorCollection<T> : ToMirror where T : IIncrementalCollectionObject
        {
            /// <inheritdoc/>
            public override bool ProcessChanges(JObject changesJson)
            {
                var update = changesJson.ToObject<IncrementalCollectionUpdate<T>>(Json.Serializer);
                UpdateCallback(update);
                return true;
            }

            /// <summary>
            /// Callback executed to update our mirror data from the json representation of MissionControl.
            /// </summary>
            /// <remarks><see cref="Application.m_Lock"/> is already locked when called and so
            /// <see cref="m_MissionControlMirror"/> can be changed.</remarks>
            public Action<IncrementalCollectionUpdate<T>> UpdateCallback { get; set; }
        }

        /// <summary>
        /// Stores information necessary to process a message received from a capsule.
        /// </summary>
        class MessageFromCapsule
        {
            /// <summary>
            /// Identifier of the Launchpad that launched the capsule.
            /// </summary>
            public Guid LaunchPadId { get; set; }
            /// <summary>
            /// Received message identifier.
            /// </summary>
            public Guid MessageId { get; set; }
            /// <summary>
            /// Stream from which to read the message and on which to send the answer.
            /// </summary>
            public NetworkStream NetworkStream { get; set; }
            /// <summary>
            /// To be executed once we are done processing the message.
            /// </summary>
            public Action PostProcess { get; set; }
        }

        /// <summary>
        /// List of single objects (not collections) we update from changes in MissionControl
        /// </summary>
        readonly List<ToMirror> m_ObjectsToMirror;
        /// <summary>
        /// List of collections we update from changes in MissionControl
        /// </summary>
        readonly List<ToMirror> m_CollectionsToMirror;
        /// <summary>
        /// List of processes to execute every time something might need to be executed.
        /// </summary>
        readonly List<IApplicationProcess> m_Processes;
        /// <summary>
        /// Objects responsible for processing messages from the capsule.
        /// </summary>
        readonly Dictionary<Guid, ICapsuleMessageProcessor> m_CapsuleMessageProcessors = new();
        /// <summary>
        /// Main <see cref="HttpClient"/> used to communicate with MissionControl.
        /// </summary>
        readonly HttpClient m_HttpClient = new();

        /// <summary>
        /// Synchronization object to use to synchronize access to the variables below
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// Is the application started?
        /// </summary>
        bool m_Started;

        /// <summary>
        /// ConditionVariable that gets signaled as soon as something changes and a new processing loop of the
        /// application must be executed.
        /// </summary>
        AsyncConditionVariable m_StartProcessingLoop = new();

        /// <summary>
        /// Messages received from launched capsules.
        /// </summary>
        List<MessageFromCapsule> m_MessagesFromCapsules = new();

        /// <summary>
        /// Stores the states of data structures mirror from MissionControl.
        /// </summary>
        MissionControlMirror m_MissionControlMirror;

        /// <summary>
        /// Canceled when the application is requested to stop.
        /// </summary>
        CancellationTokenSource m_StopApplication = new();
    }
}
