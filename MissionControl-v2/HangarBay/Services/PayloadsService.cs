using System.Diagnostics;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class PayloadsServiceExtension
    {
        public static void AddPayloadsService(this IServiceCollection services)
        {
            services.AddSingleton<PayloadsService>();
        }
    }

    /// <summary>
    /// Service taking care of managing the list of payloads.
    /// </summary>
    public class PayloadsService
    {
        public PayloadsService(IConfiguration configuration, ILogger<PayloadsService> logger,
            FileBlobCacheService fileBlobCache, HttpClient httpClient)
        {
            m_HttpClient = httpClient;

            m_Manager = fileBlobCache.NewPayloadsManager(logger, configuration["payloadsCatalog"]);
            m_Manager.FetchFileCallback = FetchPayload;
        }

        /// <summary>
        /// Gets the <see cref="Payload"/> with the specified identifier.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/> identifier.</param>
        /// <param name="payloadSource">URI from where to download payloads if they are not already available in the
        /// cache.</param>
        /// <returns><see cref="Task"/> that will provide the <see cref="Payload"/> once completed.</returns>
        public Task<Payload> GetPayload(Guid payloadIdentifier, string payloadSource)
        {
            return m_Manager.GetPayload(payloadIdentifier, payloadSource);
        }

        /// <summary>
        /// Callback used when <see cref="PayloadsManager"/> needs to fetch a payload.
        /// </summary>
        /// <param name="payloadIdentifier">Identifier of the payload to fetch.</param>
        /// <param name="cookie">Cookie passed to <see cref="PayloadsManager.GetPayload(Guid, object?)"/>.</param>
        /// <returns><see cref="Task"/> that will provide the <see cref="Payload"/> once completed.</returns>
        async Task<Payload> FetchPayload(Guid payloadIdentifier, object? cookie)
        {
            Debug.Assert(cookie != null); // GetPayload always passes a fetchCookie
            string payloadSource = (string)cookie;

            var fetched = await m_HttpClient.GetFromJsonAsync<Payload>(
                new Uri(new Uri(payloadSource), $"api/v1/payloads/{payloadIdentifier}"));
            if (fetched == null)
            {
                throw new NullReferenceException($"Failed to fetch {payloadIdentifier} from {payloadSource} (received null).");
            }
            return fetched;
        }

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// The object doing most of the work
        /// </summary>
        PayloadsManager m_Manager;
    }
}
