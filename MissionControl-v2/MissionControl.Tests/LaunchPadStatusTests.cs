using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
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

            statusA.IsDefined = true;
            statusB.IsDefined = false;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.IsDefined = true;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.UpdateError = "Something failed...";
            statusB.UpdateError = "But it is not that bad.";
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.UpdateError = "Something failed...";
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.Version = "1.2.3";
            statusB.Version = "1.2.3 beta";
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.Version = "1.2.3";
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.StartTime = DateTime.Now;
            statusB.StartTime = DateTime.Now + TimeSpan.FromHours(1);
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.StartTime = statusA.StartTime;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.State = ClusterDisplay.MissionControl.LaunchPad.State.Idle;
            statusB.State = ClusterDisplay.MissionControl.LaunchPad.State.Launched;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.State = statusA.State;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.PendingRestart = true;
            statusB.PendingRestart = false;
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.PendingRestart = true;
            Assert.That(statusA, Is.EqualTo(statusB));

            statusA.LastChanged = DateTime.Now;
            statusB.LastChanged = DateTime.Now + TimeSpan.FromHours(1);
            Assert.That(statusA, Is.Not.EqualTo(statusB));
            statusB.LastChanged = statusA.LastChanged;
            Assert.That(statusA, Is.EqualTo(statusB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchPadStatus toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadStatus>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchPadStatus toSerialize = new(Guid.NewGuid());
            toSerialize.IsDefined = true;
            toSerialize.UpdateError = "Error message";
            toSerialize.Version = "1.2.3";
            toSerialize.StartTime = DateTime.Now;
            toSerialize.State = ClusterDisplay.MissionControl.LaunchPad.State.Launched;
            toSerialize.PendingRestart = true;
            toSerialize.LastChanged = DateTime.Now - TimeSpan.FromHours(1);
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadStatus>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
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
            toClone.IsDefined = true;
            toClone.UpdateError = "Error message";
            toClone.Version = "1.2.3";
            toClone.StartTime = DateTime.Now;
            toClone.State = ClusterDisplay.MissionControl.LaunchPad.State.Launched;
            toClone.PendingRestart = true;
            toClone.LastChanged = DateTime.Now - TimeSpan.FromHours(1);
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
