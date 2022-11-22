using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class CommandTests
    {
        [Test]
        public void Prepare()
        {
            var toSerialize = new PrepareCommand();
            toSerialize.PayloadIds = new[] {Guid.NewGuid(), Guid.NewGuid()};
            toSerialize.PayloadSource = new Uri("http://mission-control-server:8000");
            toSerialize.LaunchableData = new { SomeString = "SomeValue", SomeInt = 42 };
            toSerialize.LaunchData = new { SomeOtherString = "SomeOtherValue", SomeOtherInt = 28 };
            toSerialize.PreLaunchPath = "prelaunch.ps1";
            toSerialize.LaunchPath = "ClusterDisplayReady.exe";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Launch()
        {
            var toSerialize = new LaunchCommand();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Abort()
        {
            var toSerialize = new AbortCommand();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Clear()
        {
            var toSerialize = new ClearCommand();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Shutdown()
        {
            var toSerialize = new ShutdownCommand();
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Restart()
        {
            var toSerialize = new RestartCommand();
            toSerialize.TimeoutSec = 42;
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Upgrade()
        {
            var toSerialize = new UpgradeCommand();
            toSerialize.NewVersionUrl = "http://mission-control-server:8000/hangarbay.zip";
            toSerialize.TimeoutSec = 42;
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
