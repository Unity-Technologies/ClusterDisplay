using System.Net;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchPad;

using LaunchPadCommand = Unity.ClusterDisplay.MissionControl.LaunchPad.Command;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    class LaunchPadStub
    {
        public LaunchPadStub(int port)
        {
            m_Endpoint = new Uri($"http://127.0.0.1:{port}/");
            m_HttpListener.Prefixes.Add(m_Endpoint.ToString());
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

        public ClusterDisplay.MissionControl.LaunchPad.Status Status
        {
            get
            {
                ClusterDisplay.MissionControl.LaunchPad.Status ret = new();
                lock (m_Lock)
                {
                    ret.DeepCopyFrom(m_Status);
                }
                return ret;
            }
            set
            {
                lock (m_Lock)
                {
                    ulong previousStatusNumber = m_Status.StatusNumber;
                    m_Status.DeepCopyFrom(value);
                    m_Status.StatusNumber = previousStatusNumber + 1;
                    m_StatusChanged?.TrySetResult();
                    m_StatusChanged = null;
                }
            }
        }

        public void SetHealth(Health health)
        {
            lock (m_Lock)
            {
                m_Health.DeepCopyFrom(health);
            }
        }

        public Guid Id => m_Id;

        public Uri EndPoint => m_Endpoint;

        // ReSharper disable once MemberCanBePrivate.Global -> Need to be public for the day we will start to use it.
        public Action<string, HttpMethod, HttpListenerResponse> FallbackHandler { get; set; } =
            (_, _, response) => Respond(response, HttpStatusCode.NotFound);

        public Func<LaunchPadCommand, HttpStatusCode>? CommandHandler { get; set; }

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
            var response = context.Response;

            var requestedUri = m_Endpoint.MakeRelativeUri(request.Url!).ToString();
            var httpMethod = new HttpMethod(request.HttpMethod);

            if (requestedUri.StartsWith("api/v1/status"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessStatusGet(request, response);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/health"))
            {
                if (httpMethod == HttpMethod.Get)
                {
                    ProcessHealthGet(request, response);
                }
                else
                {
                    Respond(response, HttpStatusCode.MethodNotAllowed);
                }
            }
            else if (requestedUri.StartsWith("api/v1/commands"))
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

        void ProcessStatusGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            var minStatusNumberString = request.QueryString.Get("minStatusNumber");
            if (string.IsNullOrEmpty(minStatusNumberString))
            {
                lock (m_Lock)
                {
                    JsonSerializer.Serialize(response.OutputStream, m_Status, Json.SerializerOptions);
                    Respond(response, HttpStatusCode.OK);
                }
            }
            else
            {
                ulong fromVersion = Convert.ToUInt64(minStatusNumberString);
                Task.Run(() => BlockingProcessStatusGet(request, response, fromVersion));
            }
        }

        // ReSharper disable once UnusedParameter.Local -> To keep symmetry with the other REST handlers
        async Task BlockingProcessStatusGet(HttpListenerRequest request, HttpListenerResponse response, ulong fromVersion)
        {
            for (; ; )
            {
                Task toWaitOn;
                lock (m_Lock)
                {
                    if (m_Status.StatusNumber >= fromVersion)
                    {
                        JsonSerializer.Serialize(response.OutputStream, m_Status, Json.SerializerOptions);
                        Respond(response, HttpStatusCode.OK);
                        return;
                    }
                    else
                    {
                        toWaitOn = (m_StatusChanged ??= new()).Task;
                    }
                }

                await toWaitOn;
            }
        }

        // ReSharper disable once UnusedParameter.Local -> To keep symmetry with the other REST handlers
        void ProcessHealthGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            lock (m_Lock)
            {
                JsonSerializer.Serialize(response.OutputStream, m_Health, Json.SerializerOptions);
                Respond(response, HttpStatusCode.OK);
            }
        }

        void ProcessCommandPost(HttpListenerRequest request, HttpListenerResponse response)
        {
            lock (m_Lock)
            {
                if (CommandHandler != null)
                {
                    var command = JsonSerializer.Deserialize<LaunchPadCommand>(request.InputStream,
                        Json.SerializerOptions);
                    Respond(response, command != null ? CommandHandler(command) : HttpStatusCode.BadRequest);
                }
                else
                {
                    Respond(response, HttpStatusCode.NotImplemented);
                }
            }
        }

        static void Respond(HttpListenerResponse response, HttpStatusCode statusCode)
        {
            response.StatusCode = (int)statusCode;
            try
            {
                response.Close();
            }
            catch
            {
                // Ignored
            }
        }

        readonly Guid m_Id = Guid.NewGuid();
        readonly HttpListener m_HttpListener = new();
        readonly Uri m_Endpoint;

        readonly object m_Lock = new object();

        readonly ClusterDisplay.MissionControl.LaunchPad.Status m_Status = new();
        TaskCompletionSource? m_StatusChanged;

        readonly Health m_Health = new();
    }
}
