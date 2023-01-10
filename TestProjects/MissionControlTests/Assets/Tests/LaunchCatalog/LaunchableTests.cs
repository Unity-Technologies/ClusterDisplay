using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
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

            launchableA.Type = Launchable.ClusterNodeType;
            launchableB.Type = "liveEdit";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Type = Launchable.ClusterNodeType;
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Data = new JObject();
            launchableA.Data["property"] = "valueA";
            launchableB.Data = new JObject();
            launchableB.Data["property"] = "valueB";
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Data["property"] = "valueA";
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.GlobalParameters = new() { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.GlobalParameters = new() { new LaunchParameter() { Name = "Other global parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.GlobalParameters = new() { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchComplexParameters = new() { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchComplexParameters = new() { new LaunchParameter() { Name = "Other launch complex parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchComplexParameters = new() { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.LaunchPadParameters = new() { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            launchableB.LaunchPadParameters = new() { new LaunchParameter() { Name = "Other launchpad parameter", Type = LaunchParameterType.Float, DefaultValue = 42.28 } };
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LaunchPadParameters = new() { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
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

            launchableA.LandingTime = TimeSpan.FromSeconds(2);
            launchableB.LandingTime = TimeSpan.FromSeconds(2.5);
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.LandingTime = TimeSpan.FromSeconds(2);
            Assert.That(launchableA, Is.EqualTo(launchableB));

            launchableA.Payloads.AddRange(new[] { "Payload1", "Payload2" });
            launchableB.Payloads.AddRange(new[] {"Payload3"});
            Assert.That(launchableA, Is.Not.EqualTo(launchableB));
            launchableB.Payloads.Clear();
            launchableB.Payloads.AddRange(new[] { "Payload1", "Payload2" });
            Assert.That(launchableA, Is.EqualTo(launchableB));
        }

        [Test]
        public void RoundTrip()
        {
            Launchable toSerialize = new();
            toSerialize.Name = "Cluster Node";
            toSerialize.Type = Launchable.ClusterNodeType;
            toSerialize.Data = new JObject();
            toSerialize.Data["property"] = "valueA";
            toSerialize.GlobalParameters = new() { new LaunchParameter() { Name = "Global parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchComplexParameters = new() { new LaunchParameter() { Name = "Launch complex parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.LaunchPadParameters = new() { new LaunchParameter() { Name = "Launchpad parameter", Type = LaunchParameterType.Integer, DefaultValue = 42 } };
            toSerialize.PreLaunchPath = "prelaunch.ps1";
            toSerialize.LaunchPath = "QuadroSyncTests.exe";
            toSerialize.LandingTime = TimeSpan.FromSeconds(2);
            toSerialize.Payloads.AddRange(new[] { "Payload1", "Payload2" });

            var jsonString = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<Launchable>(jsonString, Json.SerializerOptions);

            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void Deserialize()
        {
            var jsonString = ("{'name': 'Cluster Node', 'type': 'clusterNode', 'data': 42," +
                "'globalParameters': [{'name': 'Global parameter', 'type': 'integer'}]," +
                "'launchComplexParameters': [{'name': 'Launch complex parameter', 'type': 'integer'}]," +
                "'launchPadParameters': [{'name': 'Launchpad parameter', 'type': 'integer'}]," +
                "'preLaunchPath': 'prelaunch.ps1', 'launchPath': 'QuadroSyncTests.exe', 'landingTimeSec': 2, " +
                "'payloads': ['Payload1', 'Payload2']}").Replace('\'', '"');
            var deserialized = JsonConvert.DeserializeObject<Launchable>(jsonString, Json.SerializerOptions)!;

            Assert.That(deserialized.Name, Is.EqualTo("Cluster Node"));
            Assert.That(deserialized.Type, Is.EqualTo(Launchable.ClusterNodeType));
            Assert.That(deserialized.Data.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(deserialized.Data.Value<int>(), Is.EqualTo(42));
            Assert.That(deserialized.GlobalParameters.Count, Is.EqualTo(1));
            Assert.That(deserialized.GlobalParameters[0].Name, Is.EqualTo("Global parameter"));
            Assert.That(deserialized.LaunchComplexParameters.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchComplexParameters[0].Name, Is.EqualTo("Launch complex parameter"));
            Assert.That(deserialized.LaunchPadParameters.Count, Is.EqualTo(1));
            Assert.That(deserialized.LaunchPadParameters[0].Name, Is.EqualTo("Launchpad parameter"));
            Assert.That(deserialized.PreLaunchPath, Is.EqualTo("prelaunch.ps1"));
            Assert.That(deserialized.LaunchPath, Is.EqualTo("QuadroSyncTests.exe"));
            Assert.That(deserialized.LandingTime, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(deserialized.Payloads.Count, Is.EqualTo(2));
            Assert.That(deserialized.Payloads[0], Is.EqualTo("Payload1"));
            Assert.That(deserialized.Payloads[1], Is.EqualTo("Payload2"));
        }
    }
}
