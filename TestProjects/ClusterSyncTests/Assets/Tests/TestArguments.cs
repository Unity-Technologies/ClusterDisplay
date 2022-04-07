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
        private const string commonArguments =
                "-clusterNode " +
                "-batchMode " +
                "-replaceHeadlessEmitter " +
                "-delayRepeaters " +
                "-gridSize 2x2 " +
                "-bezel 4x4 " +
                "-linesAngle 1.2345 " +
                "-linesRotationSpeed 1.2345 " +
                "-linesScale 1.2345 " +
                "-linesShiftSpeed 1.2345 " +
                "-linesThickness 1.2345 " +
                "-physicalScreenSize 1920x1080 " +
                "-targetFps 60 -overscan 128 " +
                "-quadroSyncInitDelay 66 " +
                "-linesThickness 1.2345 " +
                "-linesScale 1.2345 " +
                "-linesShiftSpeed 1.2345 " +
                "-linesAngle 1.2345 " +
                "-linesRotationSpeed 1.2345 " +
                "-adapterName Ethernet " +
                "-handshakeTimeout 5000 " +
                "-communicationTimeout 5000 " +
                "-adapterName Ethernet ";

        [Test]
        public void TestCommonArguments()
        {
            var cmd = commonArguments + "-emitterNode 0 4 224.0.1.0:25691,25692";

            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.debugFlag.Defined);
            Assert.That(CommandLineParser.debugFlag.Value);

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

            Assert.That(CommandLineParser.quadroSyncInitDelay.Defined);
            Assert.That(CommandLineParser.quadroSyncInitDelay.Value == 66);

            Assert.That(CommandLineParser.adapterName.Defined);
            Assert.That(CommandLineParser.adapterName.Value == "Ethernet");

            Assert.That(CommandLineParser.handshakeTimeout.Defined);
            Assert.That(CommandLineParser.handshakeTimeout.Value == 5000);

            Assert.That(CommandLineParser.communicationTimeout.Defined);
            Assert.That(CommandLineParser.communicationTimeout.Value == 5000);

            Assert.That(CommandLineParser.linesAngle.Defined);
            Assert.That(CommandLineParser.linesAngle.Value == 1.2345f);

            Assert.That(CommandLineParser.linesRotationSpeed.Defined);
            Assert.That(CommandLineParser.linesRotationSpeed.Value == 1.2345f);

            Assert.That(CommandLineParser.linesScale.Defined);
            Assert.That(CommandLineParser.linesScale.Value == 1.2345f);

            Assert.That(CommandLineParser.linesShiftSpeed.Defined);
            Assert.That(CommandLineParser.linesShiftSpeed.Value == 1.2345f);

            Assert.That(CommandLineParser.linesThickness.Defined);
            Assert.That(CommandLineParser.linesThickness.Value == 1.2345f);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void TestEmitterArguments()
        {
            var cmd = commonArguments + "-emitterNode 0 4 224.0.1.0:25691,25692";
            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.emitterSpecified.Defined);
            Assert.That(CommandLineParser.emitterSpecified.Value);

            Assert.That(CommandLineParser.nodeID.Defined);
            Assert.That(CommandLineParser.nodeID.Value == 0);

            Assert.That(CommandLineParser.repeaterCount.Defined);
            Assert.That(CommandLineParser.repeaterCount.Value == 4);

            Assert.That(CommandLineParser.multicastAddress.Defined);
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0");

            Assert.That(CommandLineParser.rxPort.Defined);
            Assert.That(CommandLineParser.rxPort.Value == 25691);

            Assert.That(CommandLineParser.txPort.Defined);
            Assert.That(CommandLineParser.txPort.Value == 25692);
        }

        [Test]
        public void TestRepeaterArguments()
        {
            var cmd = commonArguments + "-node 1 224.0.1.0:25692,25691";
            CommandLineParser.Override(cmd.Split(' ').ToList());

            Assert.That(CommandLineParser.repeaterSpecified.Defined);
            Assert.That(CommandLineParser.repeaterSpecified.Value);

            Assert.That(CommandLineParser.nodeID.Defined);
            Assert.That(CommandLineParser.nodeID.Value == 1);

            Assert.That(CommandLineParser.multicastAddress.Defined);
            Assert.That(CommandLineParser.multicastAddress.Value == "224.0.1.0");

            Assert.That(CommandLineParser.rxPort.Defined);
            Assert.That(CommandLineParser.rxPort.Value == 25692);

            Assert.That(CommandLineParser.txPort.Defined);
            Assert.That(CommandLineParser.txPort.Value == 25691);
        }
    }
}
