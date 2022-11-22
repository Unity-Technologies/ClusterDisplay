using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    class ConstraintJsonConverter : JsonConverter<Constraint>
    {
        public override bool CanConvert(Type type)
        {
            return typeof(Constraint).IsAssignableFrom(type);
        }

        public override Constraint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
                            "range" => JsonSerializer.Deserialize<RangeConstraint>(rootElement, options),
                            "regularExpression" => JsonSerializer.Deserialize<RegularExpressionConstraint>(rootElement, options),
                            "list" => JsonSerializer.Deserialize<ListConstraint>(rootElement, options),
                            _ => throw new JsonException($"{typeValue} is not a known command type.")
                        };
                }

                throw new JsonException("Failed to extract command type property.");
            }

            throw new JsonException($"{nameof(ConstraintJsonConverter)} failed to parse JsonDocument.");
        }

        public override void Write(Utf8JsonWriter writer, Constraint value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
