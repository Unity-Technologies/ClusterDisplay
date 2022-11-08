using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Various helpers to make using HttpClient simpler.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Make a get http request to the given URL and parse the json returned object.
        /// </summary>
        /// <param name="httpClient">The <see cref="httpClient"/> to use to send the request.</param>
        /// <param name="requestUri">The URL to get.</param>
        /// <typeparam name="T">Type of object to create from the json (parsed using Json.Net).</typeparam>
        public static async Task<T> GetFromJsonAsync<T>(this HttpClient httpClient, string requestUri)
        {
            var response = await httpClient.GetAsync(requestUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType.MediaType != k_ApplicationJson)
            {
                throw new FormatException("Expects to receive a json as a response");
            }
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(responseBody, Json.SerializerOptions);
        }

        /// <summary>
        /// Do a put http request to the given URL sending the provided object as json.
        /// </summary>
        /// <param name="httpClient">The <see cref="httpClient"/> to use to send the request.</param>
        /// <param name="requestUri">The URL to get.</param>
        /// <param name="value">The object to put.</param>
        /// <typeparam name="T">Type of object to put (serialized using Json.Net).</typeparam>
        public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient httpClient, string requestUri, T value)
        {
            var serialized = JsonConvert.SerializeObject(value, Json.SerializerOptions);
            using StringContent contentToPut = new(serialized);
            contentToPut.Headers.ContentType = new MediaTypeHeaderValue(k_ApplicationJson);
            return await httpClient.PutAsync(requestUri, contentToPut);
        }

        /// <summary>
        /// Do a post http request to the given URL sending the provided object as json.
        /// </summary>
        /// <param name="httpClient">The <see cref="httpClient"/> to use to send the request.</param>
        /// <param name="requestUri">The URL to get.</param>
        /// <param name="value">The object to put.</param>
        /// <typeparam name="T">Type of object to post (serialized using Json.Net).</typeparam>
        public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient httpClient, string requestUri, T value)
        {
            var serialized = JsonConvert.SerializeObject(value, Json.SerializerOptions);
            using StringContent contentToPost = new(serialized);
            contentToPost.Headers.ContentType = new MediaTypeHeaderValue(k_ApplicationJson);
            return await httpClient.PostAsync(requestUri, contentToPost);
        }

        const string k_ApplicationJson = "application/json";
    }
}
