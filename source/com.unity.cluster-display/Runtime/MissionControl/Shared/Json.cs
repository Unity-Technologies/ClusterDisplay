using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class Json
    {
        public static JsonSerializerSettings SerializerOptions { get; } = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>() {new StringEnumConverter() {NamingStrategy = new CamelCaseNamingStrategy()}}
        };

        public static JsonSerializer Serializer { get; } = new();

        static Json()
        {
            Serializer.ContractResolver = new DefaultContractResolver() {NamingStrategy = new CamelCaseNamingStrategy()};
            Serializer.NullValueHandling = NullValueHandling.Ignore;
            Serializer.Converters.Add(new StringEnumConverter() {NamingStrategy = new CamelCaseNamingStrategy()});
        }
    }
}
