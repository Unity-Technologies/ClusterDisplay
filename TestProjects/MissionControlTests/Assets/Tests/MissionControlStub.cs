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
            LaunchComplexes.SomethingChanged += _ => m_LaunchComplexesCv.Signal();
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

        public IncrementalCollection<LaunchComplex> LaunchComplexes { get; set; } = new();
        public IncrementalCollection<LaunchParameterForReview> LaunchParametersForReview { get; set; } = new();

        // ReSharper disable once MemberCanBePrivate.Global
        public Action<string, HttpMethod, HttpListenerResponse> FallbackHandler { get; set; } =
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
            else
            {
                FallbackHandler(requestedUri, httpMethod, response);
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
                if (name != k_CapcomUplinkName)
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
                        updates[k_CapcomUplinkName] =
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
            var tokens = requestUri.Split(new[] {'?', '&', '='}).ToList();

            int name0Index = tokens.IndexOf("name0");
            if (name0Index < 0 || name0Index + 1 >= tokens.Count)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }
            var name = tokens[name0Index + 1];
            if (name != k_ComplexesName)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            int fromVersion0Index = tokens.IndexOf("fromVersion0");
            if (fromVersion0Index < 0 || fromVersion0Index + 1 >= tokens.Count)
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }
            var fromVersionString = tokens[fromVersion0Index + 1];
            ulong fromVersion = Convert.ToUInt64(fromVersionString);

            for (;;)
            {
                Task toWaitOn;
                lock (m_Lock)
                {
                    if (LaunchComplexes.VersionNumber >= fromVersion)
                    {
                        Dictionary<string, IncrementalCollectionUpdate<LaunchComplex>> updates = new();
                        updates[k_ComplexesName] = LaunchComplexes.GetDeltaSince(fromVersion);
                        RespondJson(response, updates);
                        return;
                    }
                    else
                    {
                        toWaitOn = m_LaunchComplexesCv.SignaledTask;
                    }
                }

                await toWaitOn.ConfigureAwait(false);
            }
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

        static void Respond(HttpListenerResponse response, HttpStatusCode statusCode)
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

        const string k_HttpListenerEndpoint = "http://localhost:8000/";
        const string k_CapcomUplinkName = "capcomUplink";
        const string k_ComplexesName = "complexes";
        const string k_ApplicationJson = "application/json";

        HttpListener m_HttpListener = new();

        object m_Lock = new();
        CapcomUplink m_CapcomUplink = new() {IsRunning = true};
        ulong m_CapcomUplinkVersion;
        AsyncConditionVariable m_CapcomUplinkCv = new();
        AsyncConditionVariable m_LaunchComplexesCv = new();
    }
}
