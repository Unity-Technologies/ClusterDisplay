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
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }

        static readonly JsonSerializerOptions k_SerializerOptions = new();
    }
}
