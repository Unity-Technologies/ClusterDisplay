using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    class MissionCommandJsonConverter : JsonConverter<MissionCommand>
    {
        public override bool CanConvert(Type type)
        {
            return typeof(MissionCommand).IsAssignableFrom(type);
        }

        public override MissionCommand? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                if (doc.RootElement.TryGetProperty("type", out var type))
                {
                    if (type.ValueKind == JsonValueKind.Number)
                    {
                        throw new JsonException($"Looks like the type property is an integer instead of a string, " +
                            $"was the mission command serialized using " +
                            $"{nameof(Json)}.{nameof(Json.SerializerOptions)}?");
                    }

                    var typeValue = type.GetString();
                    var rootElement = doc.RootElement.GetRawText();
                    return typeValue switch
                        {
                            "save" => JsonSerializer.Deserialize<SaveMissionCommand>(rootElement, options),
                            "load" => JsonSerializer.Deserialize<LoadMissionCommand>(rootElement, options),
                            "launch" => JsonSerializer.Deserialize<LaunchMissionCommand>(rootElement, options),
                            "stop" => JsonSerializer.Deserialize<StopMissionCommand>(rootElement, options),
                            _ => throw new JsonException($"{typeValue} is not a known mission command type.")
                        };
                }

                throw new JsonException("Failed to extract mission command type property.");
            }

            throw new JsonException($"{nameof(MissionCommandJsonConverter)} failed to parse JsonDocument.");
        }

        public override void Write(Utf8JsonWriter writer, MissionCommand value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
