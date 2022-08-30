using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
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
            toSerialize.PayloadSource = "http://mission-control-server:8000";
            toSerialize.Path = "C:\\MissionControl\\LaunchPad39A\\";
            var serializedCommand = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Command>(serializedCommand, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
