using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.MissionControl;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Helper class that mimics the MissionControl rest interface to allow testing HangarBay code.
    /// </summary>
    class MissionControlStub
    {
        public static Uri HttpListenerEndpoint => new Uri(k_HttpListenerEndpoint);

        public MissionControlStub()
        {
            m_HttpListener.Prefixes.Add(k_HttpListenerEndpoint);
            LaunchComplexes.SomethingChanged += _ => m_IncrementalCollectionChangedCv.Signal();
            LaunchParametersForReview.SomethingChanged += _ => m_IncrementalCollectionChangedCv.Signal();
            MissionParameters.SomethingChanged += _ => m_IncrementalCollectionChangedCv.Signal();
            MissionParametersDesiredValues.SomethingChanged += _ => m_IncrementalCollectionChangedCv.Signal();
            MissionParametersEffectiveValues.SomethingChanged += _ => m_IncrementalCollectionChangedCv.Signal();
        }

        public void Start()
        {
            Assert.That(m_HttpListener.IsListening, Is.False);
            m_HttpListener.Start();
            m_HttpListener.GetContextAsync().ContinueWith(ProcessRequestTask);
        }

        public void Stop()
        {
            Assert.That(m_HttpListener.IsListening, Is.True);
            m_HttpListener.Stop();
        }

        public LaunchConfiguration LaunchConfiguration { get; set; } = new();

        public CapcomUplink CapcomUplink
        {
            get
            {
                lock (m_Lock)
                {
                    return m_CapcomUplink;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    ++m_CapcomUplinkVersion;
                    m_CapcomUplink.DeepCopyFrom(value);
                    m_CapcomUplinkCv.Signal();
                }
            }
        }

        public IncrementalCollection<LaunchComplex> LaunchComplexes { get; } = new();
        public IncrementalCollection<LaunchParameterForReview> LaunchParametersForReview { get; } = new();
        public IncrementalCollection<MissionParameter> MissionParameters { get; } = new();
        public IncrementalCollection<MissionParameterValue> MissionParametersDesiredValues { get; } = new();
        public IncrementalCollection<MissionParameterValue> MissionParametersEffectiveValues { get; } = new();

        // ReSharper disable once MemberCanBePrivate.Global
        public Action<string, HttpListenerRequest, HttpListenerResponse> FallbackHandler { get; set; } =
            (_, _, response) => Respond(response, HttpStatusCode.NotFound);

        void ProcessRequestTask(Task<HttpListenerContext> task)
        {
            if (!task.IsFaulted)
            {
                try
                {
                    ProcessRequest(task.Result);
                    _ = m_HttpListener.GetContextAsync().ContinueWith(ProcessRequestTask);
                }
                catch
                {
                    try
                    {
                        Respond(task.Result.Response, HttpStatusCode.InternalServerError);
                        _ = m_HttpListener.GetContextAsync().ContinueWith(ProcessRequestTask);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            Assert.That(request, Is.Not.Null);
            var response = context.Response;
            Assert.That(response, Is.Not.Null);

            var requestedUri = new Uri(k_HttpListenerEndpoint).MakeRelativeUri(request.Url!).ToString();
            var httpMethod = new HttpMethod(request.HttpMethod);

            if (requestedUri.StartsWith("api/v1/complexes"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessComplexesGet(response);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/currentMission/launchConfiguration"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessLaunchConfigurationGet(response);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/objectsUpdate"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    _ = ProcessObjectsUpdateGet(response, requestedUri).ConfigureAwait(false);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/incrementalCollectionsUpdate"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    _ = ProcessIncrementalCollectionsUpdateGet(response, requestedUri).ConfigureAwait(false);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/currentMission/launchParametersForReview"))
            {
                if (httpMethod == HttpMethod.Put)
                {
                    _ = ProcessPutLaunchParameterForReview(response, request);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/currentMission/parametersDesiredValues"))
            {
                if (httpMethod == HttpMethod.Put)
                {
                    _ = ProcessPutMissionParameterDesiredValue(response, request);
                }
                else if (httpMethod == HttpMethod.Delete)
                {
                    ProcessDeleteMissionParameterDesiredValue(response, requestedUri);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/currentMission/parametersEffectiveValues"))
            {
                if (httpMethod == HttpMethod.Put)
                {
                    _ = ProcessPutMissionParameterEffectiveValue(response, request);
                }
                else if (httpMethod == HttpMethod.Delete)
                {
                    ProcessDeleteMissionParameterEffectiveValue(response, requestedUri);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/currentMission/parameters"))
            {
                if (httpMethod == HttpMethod.Put)
                {
                    _ = ProcessPutMissionParameter(response, request);
                }
                else if (httpMethod == HttpMethod.Delete)
                {
                    ProcessDeleteMissionParameter(response, requestedUri);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else
            {
                FallbackHandler(requestedUri, request, response);
            }
        }

        void ProcessComplexesGet(HttpListenerResponse response)
        {
            RespondJson(response, LaunchComplexes.Values);
        }

        void ProcessLaunchConfigurationGet(HttpListenerResponse response)
        {
            RespondJson(response, LaunchConfiguration);
        }

        class ObjectUpdate
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local -> Used by Json.Net
            public object Updated { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Local -> Used by Json.Net
            public ulong NextUpdate { get; set; }
        }

        async Task ProcessObjectsUpdateGet(HttpListenerResponse response, string requestUri)
        {
            var tokens = requestUri.Split(new[] {'?', '&', '='}).ToList();

            ulong fromVersion = ulong.MaxValue;
            for (int currentIndex = 0; currentIndex < 100; ++currentIndex)
            {
                int nameIndex = tokens.IndexOf($"name{currentIndex}");
                if (nameIndex < 0 || nameIndex + 1 >= tokens.Count)
                {
                    break;
                }
                var name = tokens[nameIndex + 1];
                if (name != ObservableObjectsName.CapcomUpLink)
                {   // For now we just support k_CapcomUplinkName
                    continue;
                }

                int fromVersionIndex = tokens.IndexOf($"fromVersion{currentIndex}");
                if (fromVersionIndex < 0 || fromVersionIndex + 1 >= tokens.Count)
                {
                    Respond(response, HttpStatusCode.BadRequest);
                    return;
                }
                var fromVersionString = tokens[fromVersionIndex + 1];
                fromVersion = Convert.ToUInt64(fromVersionString);
            }
            if (fromVersion == ulong.MaxValue)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            for (;;)
            {
                Task toWaitOn;
                lock (m_Lock)
                {
                    if (m_CapcomUplinkVersion >= fromVersion)
                    {
                        Dictionary<string, ObjectUpdate> updates = new();
                        updates[ObservableObjectsName.CapcomUpLink] =
                            new ObjectUpdate() { Updated = m_CapcomUplink, NextUpdate = m_CapcomUplinkVersion + 1 };
                        RespondJson(response, updates);
                        return;
                    }
                    else
                    {
                        toWaitOn = m_CapcomUplinkCv.SignaledTask;
                    }
                }

                await toWaitOn.ConfigureAwait(false);
            }
        }

        async Task ProcessIncrementalCollectionsUpdateGet(HttpListenerResponse response, string requestUri)
        {
            var kvps = requestUri.Split(new[] {'?', '&'});
            Dictionary<string, string> values = new();
            foreach (var kvp in kvps)
            {
                var split = kvp.Split('=');
                switch (split.Length)
                {
                    case 1:
                        values[split[0]] = "";
                        break;
                    case 2:
                        values[split[0]] = split[1];
                        break;
                    default:
                        Assert.Fail("Unexpected query parameter received.");
                        break;
                }
            }

            Dictionary<string, object> ret = new();

            // Get the values of requested collections
            Dictionary<string, ulong> requestedList = new();
            int collectionIndex = -1;
            for (;;)
            {
                ++collectionIndex;
                if (!values.TryGetValue($"name{collectionIndex}", out var collectionName))
                {
                    break;
                }

                if (!values.TryGetValue($"fromVersion{collectionIndex}", out var fromVersionString))
                {
                    Respond(response, HttpStatusCode.BadRequest);
                    return;
                }

                ulong fromVersion;
                try
                {
                    fromVersion = Convert.ToUInt64(fromVersionString);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                requestedList[collectionName] = fromVersion;
            }

            void ProcessCollection<T>(IncrementalCollection<T> collection, string collectionName, ulong fromVersion)
                where T : IIncrementalCollectionObject
            {
                if (collection.VersionNumber >= fromVersion)
                {
                    ret[collectionName] = collection.GetDeltaSince(fromVersion);
                }
            }

            // Wait until at least one collection has something new
            for (;;)
            {
                var collectionChangedTask = m_IncrementalCollectionChangedCv.SignaledTask;
                foreach (var request in requestedList)
                {
                    switch (request.Key)
                    {
                        case IncrementalCollectionsName.Complexes:
                            ProcessCollection(LaunchComplexes, request.Key, request.Value);
                            break;
                        case IncrementalCollectionsName.CurrentMissionLaunchParametersForReview:
                            ProcessCollection(LaunchParametersForReview, request.Key, request.Value);
                            break;
                        case IncrementalCollectionsName.CurrentMissionParameters:
                            ProcessCollection(MissionParameters, request.Key, request.Value);
                            break;
                        case IncrementalCollectionsName.CurrentMissionParametersDesiredValues:
                            ProcessCollection(MissionParametersDesiredValues, request.Key, request.Value);
                            break;
                        case IncrementalCollectionsName.CurrentMissionParametersEffectiveValues:
                            ProcessCollection(MissionParametersEffectiveValues, request.Key, request.Value);
                            break;
                    }
                }

                if (!ret.Any())
                {
                    await collectionChangedTask.ConfigureAwait(false);
                }
                else
                {
                    break;
                }
            }

            // Package response
            RespondJson(response, ret);
        }

        async Task ProcessPutLaunchParameterForReview(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (request.ContentType != k_ApplicationJson)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            string json;
            using (StreamReader reader = new(request.InputStream))
            {
                json = await reader.ReadToEndAsync();
            }

            var launchParameterForReview = JsonConvert.DeserializeObject<LaunchParameterForReview>(json,
                Json.SerializerOptions);
            if (launchParameterForReview == null)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }
            if (LaunchParametersForReview.TryGetValue(launchParameterForReview.Id,
                    out var collectionLaunchParameterForReview))
            {
                collectionLaunchParameterForReview.DeepCopyFrom(launchParameterForReview);
                Respond(response, HttpStatusCode.OK);
            }
        }

        async Task ProcessPutMissionParameter(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (request.ContentType != k_ApplicationJson)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            string json;
            using (StreamReader reader = new(request.InputStream))
            {
                json = await reader.ReadToEndAsync();
            }

            var missionParameter = JsonConvert.DeserializeObject<MissionParameter>(json, Json.SerializerOptions);
            if (missionParameter == null)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            MissionParameters[missionParameter.Id] = missionParameter;
            Respond(response, HttpStatusCode.OK);
        }

        void ProcessDeleteMissionParameter(HttpListenerResponse response, string requestUri)
        {
            var tokens = requestUri.Split(new[] {'/'}).ToList();
            var id = Guid.Parse(tokens.Last());

            Respond(response, MissionParameters.Remove(id) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        }

        async Task ProcessPutMissionParameterDesiredValue(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (request.ContentType != k_ApplicationJson)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            string json;
            using (StreamReader reader = new(request.InputStream))
            {
                json = await reader.ReadToEndAsync();
            }

            var missionParameterValue = JsonConvert.DeserializeObject<MissionParameterValue>(json, Json.SerializerOptions);
            if (missionParameterValue == null)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            MissionParametersDesiredValues[missionParameterValue.Id] = missionParameterValue;
            Respond(response, HttpStatusCode.OK);
        }

        void ProcessDeleteMissionParameterDesiredValue(HttpListenerResponse response, string requestUri)
        {
            var tokens = requestUri.Split(new[] {'/'}).ToList();
            var id = Guid.Parse(tokens.Last());

            Respond(response, MissionParametersDesiredValues.Remove(id) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        }

        async Task ProcessPutMissionParameterEffectiveValue(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (request.ContentType != k_ApplicationJson)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            string json;
            using (StreamReader reader = new(request.InputStream))
            {
                json = await reader.ReadToEndAsync();
            }

            var missionParameterValue = JsonConvert.DeserializeObject<MissionParameterValue>(json, Json.SerializerOptions);
            if (missionParameterValue == null)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            MissionParametersEffectiveValues[missionParameterValue.Id] = missionParameterValue;
            Respond(response, HttpStatusCode.OK);
        }

        void ProcessDeleteMissionParameterEffectiveValue(HttpListenerResponse response, string requestUri)
        {
            var tokens = requestUri.Split(new[] {'/'}).ToList();
            var id = Guid.Parse(tokens.Last());

            Respond(response, MissionParametersEffectiveValues.Remove(id) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        }

        public static void Respond(HttpListenerResponse response, HttpStatusCode statusCode)
        {
            response.StatusCode = (int)statusCode;
            response.Close();
        }

        static void RespondJson<T>(HttpListenerResponse response, T toSerialize, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            response.StatusCode = (int)statusCode;
            response.ContentType = k_ApplicationJson;
            using (StreamWriter responseWrite = new(response.OutputStream))
            {
                responseWrite.Write(JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions));
                responseWrite.Flush();
            }
            response.Close();
        }

        static readonly string k_HttpListenerEndpoint = $"http://localhost:{Helpers.ListenPort}/";
        const string k_ApplicationJson = "application/json";

        HttpListener m_HttpListener = new();

        object m_Lock = new();
        CapcomUplink m_CapcomUplink = new() {IsRunning = true};
        ulong m_CapcomUplinkVersion;
        AsyncConditionVariable m_CapcomUplinkCv = new();
        AsyncConditionVariable m_IncrementalCollectionChangedCv = new();
    }
}
