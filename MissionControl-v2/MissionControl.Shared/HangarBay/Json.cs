using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    public static class Json
    {
        static Json()
        {
            k_SerializerOptions = new();
            AddToSerializerOptions(k_SerializerOptions);
        }

        static public JsonSerializerOptions SerializerOptions => k_SerializerOptions;

        static public void AddToSerializerOptions(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        static readonly JsonSerializerOptions k_SerializerOptions;
    }
}
