using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class ConstraintJsonConverter: JsonConverter
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var dynamic = JObject.Load(reader);
            var typeValue = (string)dynamic["type"];
            Constraint constraintOfType = typeValue switch
            {
                "range" => new RangeConstraint(),
                "regularExpression" => new RegularExpressionConstraint(),
                "list" => new ListConstraint(),
                _ => throw new JsonException($"{typeValue} is not a known constraint type.")
            };

            serializer.Populate(dynamic.CreateReader(), constraintOfType);

            return constraintOfType;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Constraint);
        }
    }
}
