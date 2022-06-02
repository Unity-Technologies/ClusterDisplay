using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay;
using System.Linq;

namespace Unity.ClusterDisplay.Tests
{
    public class TestArguments
    {
        const string commonArguments =
                "-batchMode " +
                "-replaceHeadlessEmitter " +
                "-delayRepeaters " +
                "-gridSize 2x2 " +
                "-bezel 4x4 " +
                "-physicalScreenSize 1920x1080 " +
                "-targetFps 60 -overscan 128 " +
                "-quadroSyncInitDelay 66 " +
                "-linesThickness 1.2345 " +
                "-linesScale 1.2345 " +
                "-linesShiftSpeed 1.2345 " +
                "-linesAngle 1.2345 " +
                "-linesRotationSpeed 1.2345 " +
                "-adapterName Ethernet " +
                "-handshakeTimeout 6000 " +
                "-communicationTimeout 5000 " +
                "-adapterName Ethernet ";

        [Test]
        public void TestCommonArguments()
        {
            var cmd = commonArguments + "-emitterNode 0 4 224.0.1.0:25691";

            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.replaceHeadlessEmitter.Defined);
            Assert.That(CommandLineParser.replaceHeadlessEmitter.Value);

            Assert.That(CommandLineParser.delayRepeaters.Defined);
            Assert.That(CommandLineParser.delayRepeaters.Value);

            Assert.That(CommandLineParser.gridSize.Defined);
            Assert.That(CommandLineParser.gridSize.Value == new Vector2Int(2, 2));

            Assert.That(CommandLineParser.bezel.Defined);
            Assert.That(CommandLineParser.bezel.Value == new Vector2Int(4, 4));

            Assert.That(CommandLineParser.physicalScreenSize.Defined);
            Assert.That(CommandLineParser.physicalScreenSize.Value == new Vector2Int(1920, 1080));

            Assert.That(CommandLineParser.targetFps.Defined);
            Assert.That(CommandLineParser.targetFps.Value == 60);

            Assert.That(CommandLineParser.overscan.Defined);
            Assert.That(CommandLineParser.overscan.Value == 128);

            Assert.That(CommandLineParser.adapterName.Defined);
            Assert.That(CommandLineParser.adapterName.Value == "Ethernet");

            Assert.That(CommandLineParser.handshakeTimeout.Defined);
            Assert.That(CommandLineParser.handshakeTimeout.Value == 6000);

            Assert.That(CommandLineParser.communicationTimeout.Defined);
            Assert.That(CommandLineParser.communicationTimeout.Value == 5000);

            Assert.IsFalse(CommandLineParser.disableQuadroSync.Defined);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestEmitterArguments()
        {
            var cmd = commonArguments + "-emitterNode 0 4 224.0.1.0:25691";
            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.emitterSpecified.Defined);
            Assert.That(CommandLineParser.emitterSpecified.Value);

            Assert.That(CommandLineParser.nodeID.Defined);
            Assert.That(CommandLineParser.nodeID.Value == 0);

            Assert.That(CommandLineParser.repeaterCount.Defined);
            Assert.That(CommandLineParser.repeaterCount.Value == 4);

            Assert.That(CommandLineParser.multicastAddress.Defined);
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0");

            Assert.That(CommandLineParser.port.Defined);
            Assert.That(CommandLineParser.port.Value == 25691);
        }

        [Test]
        public void TestRepeaterArguments()
        {
            var cmd = commonArguments + "-node 1 224.0.1.0:25692";
            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.repeaterSpecified.Defined);
            Assert.That(CommandLineParser.repeaterSpecified.Value);

            Assert.That(CommandLineParser.nodeID.Defined);
            Assert.That(CommandLineParser.nodeID.Value == 1);

            Assert.That(CommandLineParser.multicastAddress.Defined);
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0");

            Assert.That(CommandLineParser.port.Defined);
            Assert.That(CommandLineParser.port.Value == 25692);
        }

        [Test]
        public void TestClusterParamsFromArguments()
        {
            var cmd = commonArguments + "-emitterNode 0 4 224.0.1.0:25690";
            CommandLineParser.Override(cmd.Split(' ').ToList());

            var clusterParams = ClusterParams.FromCommandLine();
            var expected = new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = true,
                RepeaterCount = 4,
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                AdapterName = "Ethernet",
                TargetFps = 60,
                DelayRepeaters = true,
                HeadlessEmitter = true,
                ReplaceHeadlessEmitter = true,
                HandshakeTimeout = TimeSpan.FromMilliseconds(6000),
                CommunicationTimeout = TimeSpan.FromMilliseconds(5000),
                EnableHardwareSync = true
            };
            Assert.That(clusterParams, Is.EqualTo(expected));

            cmd = "-node 4 224.0.1.2:25690 -disableQuadroSync";
            CommandLineParser.Override(cmd.Split(' ').ToList());
            clusterParams = ClusterParams.FromCommandLine();

            expected = new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = false,
                NodeID = 4,
                RepeaterCount = 0,
                Port = 25690,
                MulticastAddress = "224.0.1.2",
                TargetFps = -1,
                DelayRepeaters = false,
                HeadlessEmitter = false,
                HandshakeTimeout = TimeSpan.FromMilliseconds(10000),
                CommunicationTimeout = TimeSpan.FromMilliseconds(10000),
                EnableHardwareSync = false
            };

            Assert.That(clusterParams, Is.EqualTo(expected));
        }
    }
}
