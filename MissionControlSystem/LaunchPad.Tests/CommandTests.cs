using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public class CommandTests
    {
        [Test]
        public void Prepare()
        {
            var toSerialize = new PrepareCommand();
            toSerialize.PayloadIds = new[] {Guid.NewGuid(), Guid.NewGuid()};
            toSerialize.MissionControlEntry = new Uri("http://mission-control-server:8000");
            toSerialize.LaunchableData = JsonNode.Parse("{'SomeString': 'SomeValue', 'SomeInt': 42 }".Replace('\'', '\"'));
            toSerialize.LaunchData = JsonNode.Parse("{'SomeOtherString': 'SomeOtherValue', 'SomeOtherInt': 28 }".Replace('\'', '\"'));
            toSerialize.PreLaunchPath = "prelaunch.ps1";
            toSerialize.LaunchPath = "ClusterDisplayReady.exe";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        [TestCase("http://127.0.0.1:8000")]
        [TestCase("http://127.0.0.1:8000/")]
        public void PrepareMissionControlEntry(string uri)
        {
            PrepareCommand prepareCommand = new();
            prepareCommand.MissionControlEntry = new Uri(uri);
            Assert.That(prepareCommand.MissionControlEntry.ToString(), Is.EqualTo("http://127.0.0.1:8000/"));
            Assert.That(JsonSerializer.Serialize(prepareCommand.MissionControlEntry), Is.EqualTo("\"http://127.0.0.1:8000/\""));
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
        public void AbortToOver()
        {
            var toSerialize = new AbortCommand();
            toSerialize.AbortToOver = true;
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
