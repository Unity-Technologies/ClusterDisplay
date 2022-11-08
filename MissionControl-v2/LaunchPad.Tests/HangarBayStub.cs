using System;
using System.Net;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    class HangarBayStubCheckpoint
    {
        public Task WaitingOnCheckpoint => m_WaitingOnCheckpointTcs.Task;

        public Task PerformCheckpoint()
        {
            m_WaitingOnCheckpointTcs.TrySetResult();
            return m_CheckpointFinishedTcs.Task;
        }

        public void UnblockCheckpoint()
        {
            m_CheckpointFinishedTcs.SetResult();
        }

        TaskCompletionSource m_WaitingOnCheckpointTcs = new();
        TaskCompletionSource m_CheckpointFinishedTcs = new();
    }

    class HangarBayStub
    {
        public static string HttpListenerEndpoint => k_HttpListenerEndpoint;

        public HangarBayStub()
        {
            m_HttpListener.Prefixes.Add(k_HttpListenerEndpoint);
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
            lock (m_Lock)
            {
                m_Payloads.Clear();
            }
        }

        public void AddFile(Guid payloadId, string path, string content)
        {
            lock (m_Lock)
            {
                if (!m_Payloads.TryGetValue(payloadId, out var payloadInfo))
                {
                    payloadInfo = new();
                    m_Payloads.Add(payloadId, payloadInfo);
                }

                payloadInfo.Files.Add(new FileInfo() { Path = path, Content = content });
            }
        }

        public void AddFileToCopy(Guid payloadId, string path, string fromPath)
        {
            lock (m_Lock)
            {
                if (!m_Payloads.TryGetValue(payloadId, out var payloadInfo))
                {
                    payloadInfo = new();
                    m_Payloads.Add(payloadId, payloadInfo);
                }

                payloadInfo.Files.Add(new FileInfo() { Path = path, CopyFromPath = fromPath });
            }
        }

        public void AddPayloadCheckpoint(Guid payloadId, HangarBayStubCheckpoint checkpoint)
        {
            lock (m_Lock)
            {
                Assert.That(m_Payloads.TryGetValue(payloadId, out var payloadInfo), Is.True);
                payloadInfo!.Checkpoints.Add(checkpoint);
            }
        }

        public Action<string, HttpMethod, HttpListenerResponse> FallbackHandler { get; set; } =
            (_, _, response) => Respond(response, HttpStatusCode.NotFound);

        void ProcessRequestTask(Task<HttpListenerContext> task)
        {
            if (!task.IsFaulted)
            {
                try
                {
                    ProcessRequest(task.Result);
                    m_HttpListener.GetContextAsync().ContinueWith(ProcessRequestTask);
                }
                catch
                {
                    try
                    {
                        Respond(task.Result.Response, HttpStatusCode.InternalServerError);
                        m_HttpListener.GetContextAsync().ContinueWith(ProcessRequestTask);
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

            if (requestedUri.StartsWith("api/v1/commands"))
            {
                if (httpMethod == HttpMethod.Post)
                {
                    ProcessCommandPost(request, response);
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

        void ProcessCommandPost(HttpListenerRequest request, HttpListenerResponse response)
        {
            var cmd = JsonSerializer.Deserialize<HangarBay.Command>(request.InputStream, Json.SerializerOptions);
            var prepareCmd = cmd as HangarBay.PrepareCommand;
            if (prepareCmd == null) // So far this is the only stub supported command
            {
                Respond(response, HttpStatusCode.BadRequest);
                return;
            }

            lock (m_Lock)
            {
                foreach (var payloadId in prepareCmd.PayloadIds)
                {
                    if (!m_Payloads.TryGetValue(payloadId, out var payloadInfo))
                    {
                        Respond(response, HttpStatusCode.NotFound);
                        return;
                    }

                    // Fill the directory to prepare
                    try
                    {
                        Directory.Delete(prepareCmd.Path, true);
                        Directory.CreateDirectory(prepareCmd.Path);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                    foreach (var file in payloadInfo.Files)
                    {
                        var filePath = Path.Combine(prepareCmd.Path, file.Path);
                        if (!string.IsNullOrEmpty(file.Content))
                        {
                            File.WriteAllText(filePath, file.Content);
                        }
                        else
                        {
                            Assert.That(file.CopyFromPath, Is.Not.EqualTo(""));
                            File.Copy(file.CopyFromPath, filePath);
                        }
                    }

                    // Call checkpoints
                    var checkpointsTask = payloadInfo.Checkpoints.Select(c => c.PerformCheckpoint()).ToArray();
                    Task.WaitAll(checkpointsTask);

                    // Done
                    Respond(response, HttpStatusCode.OK);
                }
            }
        }

        static void Respond(HttpListenerResponse response, HttpStatusCode statusCode)
        {
            response.StatusCode = (int)statusCode;
            response.Close();
        }

        class FileInfo
        {
            public string Path { get; init; } = "";
            public string Content { get; init; } = "";
            public string CopyFromPath { get; init; } = "";
        }

        class PayloadInfo
        {
            public List<FileInfo> Files { get; private set; } = new();
            public List<HangarBayStubCheckpoint> Checkpoints { get; private set; } = new();
        }

        const string k_HttpListenerEndpoint = "http://127.0.0.1:8100/";
        readonly HttpListener m_HttpListener = new();

        readonly object m_Lock = new object();
        readonly Dictionary<Guid, PayloadInfo> m_Payloads = new();
    }
}
