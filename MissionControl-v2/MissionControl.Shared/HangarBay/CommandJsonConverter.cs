using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    internal class CommandJsonConverter : JsonConverter<Command>
    {
        public override bool CanConvert(Type type)
        {
            return type.IsAssignableFrom(typeof(Command));
        }

        public override Command? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                if (doc.RootElement.TryGetProperty("type", out var type))
                {
                    if (type.ValueKind == JsonValueKind.Number)
                    {
                        throw new JsonException($"Looks like the type property is an integer instead of a string, " +
                            $"was the command serialized using {nameof(Json)}.{nameof(Json.SerializerOptions)}?");
                    }

                    var typeValue = type.GetString();
                    var rootElement = doc.RootElement.GetRawText();
                    return typeValue switch
                        {
                            "prepare" => JsonSerializer.Deserialize<PrepareCommand>(rootElement, options),
                            "shutdown" => JsonSerializer.Deserialize<ShutdownCommand>(rootElement, options),
                            "restart" => JsonSerializer.Deserialize<RestartCommand>(rootElement, options),
                            "upgrade" => JsonSerializer.Deserialize<UpgradeCommand>(rootElement, options),
                            _ => throw new JsonException($"{typeValue} is not a known command type.")
                        };
                }

                throw new JsonException("Failed to extract command type property.");
            }

            throw new JsonException($"{nameof(CommandJsonConverter)} failed to parse JsonDocument.");
        }

        public override void Write(Utf8JsonWriter writer, Command value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
