using System;
using Newtonsoft.Json;

namespace Unity.ClusterDisplay.MissionControl
{
    public class TimeSpanToSecondsJsonConverter: JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var timeSpan = (TimeSpan)value!;
            writer.WriteValue(timeSpan.TotalSeconds);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return TimeSpan.FromSeconds(Convert.ToSingle(reader.Value));
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TimeSpan);
        }
    }
}
