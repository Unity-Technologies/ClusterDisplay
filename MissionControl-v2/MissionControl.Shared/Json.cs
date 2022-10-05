using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class Json
    {
        static Json()
        {
            AddToSerializerOptions(k_SerializerOptions);
        }

        public static JsonSerializerOptions SerializerOptions => k_SerializerOptions;

        public static void AddToSerializerOptions(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        static readonly JsonSerializerOptions k_SerializerOptions = new();
    }
}
