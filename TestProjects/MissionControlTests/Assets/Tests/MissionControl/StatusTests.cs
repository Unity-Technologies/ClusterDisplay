using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class StatusTests
    {
        [Test]
        public void Equal()
        {
            Status statusA = new();
            Status statusB = new();
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.State = State.Preparing;
            statusB.State = State.Launched;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.State = State.Preparing;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.EnteredStateTime = DateTime.Now;
            statusB.EnteredStateTime = DateTime.Now + TimeSpan.FromMinutes(1);
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.EnteredStateTime = statusA.EnteredStateTime;
            Assert.That(statusA, Is.EqualTo(statusB));
        }

        [Test]
        public void RoundTrip()
        {
            Status toSerialize = new();
            toSerialize.State = State.Preparing;
            toSerialize.EnteredStateTime = DateTime.Now;

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Status>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'version':'1.0.0.0','startTime':'2022-12-05T07:38:43.6823645-05:00','storageFolders':" +
                "[{'path':'C:\\\\Users\\\\frederick.stlaurent\\\\Documents\\\\Unity\\\\Mission Control Storage'," +
                "'currentSize':37764846,'zombiesSize':0,'maximumSize':650406672384}],'pendingRestart':false,'state':" +
                "'launched','enteredStateTime':'2022-12-05T07:39:25.4723335-05:00'}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Status>(jsonString, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);

            Assert.That(deserialized.State, Is.EqualTo(State.Launched));
            Assert.That(deserialized.EnteredStateTime, Is.EqualTo(DateTime.Parse("2022-12-05T07:39:25.4723335-05:00")));
        }

        [Test]
        public void DeepCopyDefault()
        {
            Status toCopy = new();
            Status copied = new();
            copied.DeepCopyFrom(toCopy);
            Assert.That(copied, Is.EqualTo(toCopy));
        }

        [Test]
        public void DeepCloneFull()
        {
            Status toCopy = new();
            toCopy.State = State.Preparing;
            toCopy.EnteredStateTime = DateTime.Now;
            Status copied = new();
            copied.DeepCopyFrom(toCopy);
            Assert.That(copied, Is.EqualTo(toCopy));
        }
    }
}
