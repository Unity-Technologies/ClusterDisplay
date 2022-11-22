using System.Text.Json;

using LaunchParameter = Unity.ClusterDisplay.MissionControl.LaunchCatalog.LaunchParameter;
using LaunchParameterType = Unity.ClusterDisplay.MissionControl.LaunchCatalog.LaunchParameterType;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class LaunchableTests
    {
        [Test]
        public void Equal()
        {
            Launchable launchableA = new();
            Launchable launchableB = new();
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Name = "Cluster Node";
            launchableB.Name = "Live Edit";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Name = "Cluster Node";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Type = "clusterNode";
            launchableB.Type = "liveEdit";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Type = "clusterNode";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Data = new { StringProperty = "StringValue", NumberProperty = 42 };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Data = launchableA.Data;
            launchableA.Data = null;
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableA.Data = new { OtherStringProperty = "OtherStringValue", OtherNumberProperty = 28 };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableA.Data = new { StringProperty = "StringValue", NumberProperty = 42.0 };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.GlobalParameters = new[] { new LaunchParameter() { Name = "Other global parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Other launch complex parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Other launchpad parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.PreLaunchPath = "prelaunch.ps1";
            launchableB.PreLaunchPath = "somethingelse.exe";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.PreLaunchPath = "prelaunch.ps1";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchPath = "QuadroSyncTests.exe";
            launchableB.LaunchPath = "SpaceshipDemo.exe";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchPath = "QuadroSyncTests.exe";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            var payload1 = Guid.NewGuid();
            var payload2 = Guid.NewGuid();
            var payload3 = Guid.NewGuid();

            launchableA.Payloads = new[] { payload1, payload2 };
            launchableB.Payloads = new[] { payload3 };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Payloads = new[] { payload1, payload2 };
            Assert.That(launchableA, Is.EqualTo(launchableB));
        }

        [Test]
        public void SerializeDefault()
        {
            Launchable toSerialize = new();
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Launchable>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            Launchable toSerialize = new();
            toSerialize.Name = "Cluster Node";
            toSerialize.Type = "clusterNode";
            toSerialize.Data = new { StringProperty = "StringValue", NumberProperty = 42 };
            toSerialize.GlobalParameters = new[] { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchComplexParameters = new[] { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchPadParameters = new[] { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.PreLaunchPath = "prelaunch.ps1";
            toSerialize.LaunchPath = "QuadroSyncTests.exe";
            toSerialize.Payloads = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var serializedParameter = JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            var deserialized = JsonSerializer.Deserialize<Launchable>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }
    }
}
