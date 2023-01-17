using System;
using Newtonsoft.Json;
using NUnit.Framework;

using LaunchPadState = Unity.ClusterDisplay.MissionControl.LaunchPad.State;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadStatusTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchPadStatus statusA = new(theId);
            LaunchPadStatus statusB = new(Guid.NewGuid());
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB = new(theId);
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.State = LaunchPadState.Launched;
            statusB.State = LaunchPadState.Over;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.State = LaunchPadState.Launched;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.IsDefined = true;
            statusB.IsDefined = false;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.IsDefined = true;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.UpdateError = "Some error...";
            statusB.UpdateError = "Some other error...";
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.UpdateError = "Some error...";
            Assert.That(statusA, Is.EqualTo(statusB));
        }

        [Test]
        public void RoundTrip()
        {
            LaunchPadStatus toSerialize = new(Guid.NewGuid());
            toSerialize.State = LaunchPadState.Launched;
            toSerialize.IsDefined = true;
            toSerialize.UpdateError = "Something is not working...";

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchPadStatus>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'id':'81cfd990-4546-4b31-86c8-d6c1b74448b0','isDefined':true," +
                "'updateError':'Something','version':'1.0.0.0','startTime':'2022-12-20T09:02:06.4258628-05:00'," +
                "'state':'over','pendingRestart':false,'lastChanged':'2022-12-21T07:31:44.7122426-05:00'," +
                "'statusNumber':76}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<LaunchPadStatus>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Id, Is.EqualTo(Guid.Parse("81cfd990-4546-4b31-86c8-d6c1b74448b0")));
            Assert.That(deserialized.State, Is.EqualTo(LaunchPadState.Over));
            Assert.That(deserialized.IsDefined, Is.True);
            Assert.That(deserialized.UpdateError, Is.EqualTo("Something"));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPadStatus toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPadStatus toClone = new(Guid.NewGuid());
            toClone.State = LaunchPadState.Launched;
            toClone.IsDefined = true;
            toClone.UpdateError = "Something is not working...";
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
