using System;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    class MissionControlStubCheckpoint
    {
        public Task WaitingOnCheckpoint => m_WaitingOnCheckpointTcs.Task;

        public Task PerformCheckpoint()
        {
            m_WaitingOnCheckpointTcs.SetResult();
            return m_CheckpointFinishedTcs.Task;
        }

        public void UnblockCheckpoint()
        {
            m_CheckpointFinishedTcs.SetResult();
        }

        TaskCompletionSource m_WaitingOnCheckpointTcs = new();
        TaskCompletionSource m_CheckpointFinishedTcs = new();
    }

    /// <summary>
    /// Helper class that mimics the MissionControl rest interface to allow testing HangarBay code.
    /// </summary>
    class MissionControlStub
    {
        public static Uri HttpListenerEndpoint => new Uri(k_HttpListenerEndpoint);

        public MissionControlStub()
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
            m_Payloads.Clear();
            m_Files.Clear();
            History.Clear();
        }

        public class HistoryEntry
        {
            public string Uri { get; init; } = "";
        }

        public void AddFile(Guid payloadId, string path, Guid fileBobId, string content, bool forceFile = false)
        {
            lock (m_Lock)
            {
                if (forceFile || !m_Files.TryGetValue(fileBobId, out var fileInfo))
                {
                    fileInfo = new();

                    using (var contentStream = new MemoryStream())
                    using (var contentStreamWriter = new StreamWriter(contentStream))
                    {
                        contentStreamWriter.Write(content);
                        contentStreamWriter.Flush();
                        contentStream.Seek(0, SeekOrigin.Begin);

                        fileInfo.Size = contentStream.Length;

                        using (var compressedStream = new MemoryStream())
                        using (var compressor = new GZipStream(compressedStream, CompressionMode.Compress))
                        {
                            contentStream.CopyTo(compressor);
                            compressor.Flush();

                            compressedStream.Seek(0, SeekOrigin.Begin);
                            fileInfo.CompressedSize = compressedStream.Length;
                            fileInfo.Data = compressedStream.ToArray();
                        }
                    }

                    if (!forceFile)
                    {
                        m_Files[fileBobId] = fileInfo;
                    }
                }

                if (!m_Payloads.TryGetValue(payloadId, out var payloadInfo))
                {
                    payloadInfo = new();
                    m_Payloads[payloadId] = payloadInfo;
                }
                payloadInfo.Files.Add(new PayloadFile(path, fileBobId, fileInfo.CompressedSize, fileInfo.Size));
            }
        }

        public void RemoveFileBlob(Guid fileBlobId)
        {
            lock (m_Lock)
            {
                m_Files.Remove(fileBlobId);
            }
        }

        public void AddPayloadCheckpoint(Guid payloadId, MissionControlStubCheckpoint checkpoint)
        {
            lock (m_Lock)
            {
                Assert.That(m_Payloads.TryGetValue(payloadId, out var payloadInfo), Is.True);
                payloadInfo!.Checkpoints.Add(checkpoint);
            }
        }

        public void AddFileCheckpoint(Guid fileBlobId, MissionControlStubCheckpoint checkpoint)
        {
            lock (m_Lock)
            {
                Assert.That(m_Files.TryGetValue(fileBlobId, out var fileInfo), Is.True);
                fileInfo!.Checkpoints.Add(checkpoint);
            }
        }

        public ConcurrentQueue<HistoryEntry> History { get; private set; } = new();

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
            Assert.That(httpMethod, Is.EqualTo(HttpMethod.Get)); // So far, all methods we are "stubbing" are gets...
            History.Enqueue(new HistoryEntry() { Uri = requestedUri });

            if (requestedUri.StartsWith("api/v1/payloads"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessPayloadGet(response, requestedUri);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/fileBlobs"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessFileBlobGet(response, requestedUri);
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

        void ProcessPayloadGet(HttpListenerResponse response, string uri)
        {
            int idStart = uri.LastIndexOf("/");
            Assert.That(idStart, Is.Not.EqualTo(-1));
            string payloadIdString = uri.Substring(idStart + 1);
            var payloadId = Guid.Parse(payloadIdString);

            lock (m_Lock)
            {
                if (!m_Payloads.TryGetValue(payloadId, out var payloadInfo))
                {
                    Respond(response, HttpStatusCode.NotFound);
                    return;
                }

                var checkpointsTask = payloadInfo.Checkpoints.Select(c => c.PerformCheckpoint()).ToArray();
                Task.WaitAll(checkpointsTask);

                RespondJson(response, payloadInfo);
            }
        }

        void ProcessFileBlobGet(HttpListenerResponse response, string uri)
        {
            int idStart = uri.LastIndexOf("/");
            Assert.That(idStart, Is.Not.EqualTo(-1));
            string fileBlobIdString = uri.Substring(idStart + 1);
            var fileBlobId = Guid.Parse(fileBlobIdString);

            lock (m_Lock)
            {
                if (!m_Files.TryGetValue(fileBlobId, out var fileInfo))
                {
                    Respond(response, HttpStatusCode.NotFound);
                    return;
                }

                var checkpointsTask = fileInfo.Checkpoints.Select(c => c.PerformCheckpoint()).ToArray();
                Task.WaitAll(checkpointsTask);

                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/gzip";
                using (var memStream = new MemoryStream(fileInfo.Data, false))
                {
                    memStream.CopyTo(response.OutputStream);
                }
                response.Close();
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
            response.ContentType = "application/json";
            JsonSerializer.Serialize(response.OutputStream, toSerialize, Json.SerializerOptions);
            response.Close();
        }

        class FileInfo
        {
            public byte[] Data { get; set; } = { };
            public long CompressedSize { get; set; }
            public long Size { get; set; }
            public List<MissionControlStubCheckpoint> Checkpoints { get; } = new();
        }

        class PayloadInfo
        {
            // ReSharper disable once CollectionNeverQueried.Local -> Used when PayloadFile are serialized
            public List<PayloadFile> Files { get; } = new();
            [JsonIgnore]
            public List<MissionControlStubCheckpoint> Checkpoints { get; } = new();
        }

        const string k_HttpListenerEndpoint = "http://localhost:8000/";
        HttpListener m_HttpListener = new();

        readonly object m_Lock = new object();
        Dictionary<Guid, FileInfo> m_Files = new();
        Dictionary<Guid, PayloadInfo> m_Payloads = new();
    }
}
