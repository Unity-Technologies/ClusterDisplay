using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    public class CommandTests
    {
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
        public void Prepare()
        {
            var toSerialize = new PrepareCommand();
            toSerialize.PayloadIds = new[] {Guid.NewGuid(), Guid.NewGuid()};
            toSerialize.PayloadSource = new Uri("http://mission-control-server:8000");
            toSerialize.Path = "C:\\MissionControl\\LaunchPad39A\\";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        [TestCase("http://127.0.0.1:8000")]
        [TestCase("http://127.0.0.1:8000/")]
        public void PreparePayloadSource(string uri)
        {
            PrepareCommand prepareCommand = new();
            prepareCommand.PayloadSource = new Uri(uri);
            Assert.That(prepareCommand.PayloadSource.ToString(), Is.EqualTo("http://127.0.0.1:8000/"));
            Assert.That(JsonSerializer.Serialize(prepareCommand.PayloadSource), Is.EqualTo("\"http://127.0.0.1:8000/\""));
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
