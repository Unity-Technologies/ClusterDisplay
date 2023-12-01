using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchPadHealthTests
    {
        [Test]
        public void Equal()
        {
            Guid theId = Guid.NewGuid();
            LaunchPadHealth healthA = new(theId);
            LaunchPadHealth healthB = new(Guid.NewGuid());
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB = new(theId);
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.IsDefined = true;
            healthB.IsDefined = false;
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.IsDefined = true;
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.UpdateError = "Something failed...";
            healthB.UpdateError = "But it is not that bad.";
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.UpdateError = "Something failed...";
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.UpdateTime = DateTime.Now;
            healthB.UpdateTime = DateTime.Now + TimeSpan.FromHours(1);
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.UpdateTime = healthA.UpdateTime;
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.CpuUtilization = 0.42f;
            healthB.CpuUtilization = 0.28f;
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.CpuUtilization = 0.42f;
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.MemoryUsage = 42 * 1024 * 1024;
            healthB.MemoryUsage = 28 * 1024 * 1024;
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.MemoryUsage = 42 * 1024 * 1024;
            Assert.That(healthA, Is.EqualTo(healthB));

            healthA.MemoryUsage = 420 * 1024 * 1024;
            healthB.MemoryUsage = 280 * 1024 * 1024;
            Assert.That(healthA, Is.Not.EqualTo(healthB));
            healthB.MemoryUsage = 420 * 1024 * 1024;
            Assert.That(healthA, Is.EqualTo(healthB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchPadHealth toSerialize = new(Guid.NewGuid());
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadHealth>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchPadHealth toSerialize = new(Guid.NewGuid());
            toSerialize.IsDefined = true;
            toSerialize.UpdateError = "Error message";
            toSerialize.UpdateTime = DateTime.Now;
            toSerialize.CpuUtilization = 0.28f;
            toSerialize.MemoryUsage = 28 * 1024 * 1024;
            toSerialize.MemoryInstalled = 42 * 1024 * 1024;
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<LaunchPadHealth>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void DeepCloneDefault()
        {
            LaunchPadHealth toClone = new(Guid.NewGuid());
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }

        [Test]
        public void DeepCloneFull()
        {
            LaunchPadHealth toClone = new(Guid.NewGuid());
            toClone.IsDefined = true;
            toClone.UpdateError = "Error message";
            toClone.UpdateTime = DateTime.Now;
            toClone.CpuUtilization = 0.28f;
            toClone.MemoryUsage = 28 * 1024 * 1024;
            toClone.MemoryInstalled = 42 * 1024 * 1024;
            var cloned = toClone.DeepClone();
            Assert.That(cloned, Is.EqualTo(toClone));
        }
    }
}
