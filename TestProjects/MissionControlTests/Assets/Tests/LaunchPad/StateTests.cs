using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public class StateTests
    {
        [Test]
        public void Equal()
        {
            Status statusA = new();
            Status statusB = new();
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.State = State.Launched;
            statusB.State = State.Over;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.State = State.Launched;
            Assert.That(statusA, Is.EqualTo(statusB));
        }

        [Test]
        public void RoundTrip()
        {
            Status toSerialize = new();
            toSerialize.State = State.Launched;

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Status>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'version':'1.0.0.0','startTime':'2022-12-20T09:02:06.4258628-05:00','state':'over'," +
                "'pendingRestart':false,'lastChanged':'2022-12-21T07:31:44.7122426-05:00'," +
                "'statusNumber':76}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Status>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.State, Is.EqualTo(State.Over));
        }
    }
}
