#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.ClusterDisplay.Scripting;
using Unity.ClusterDisplay.Utils;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Tests
{
    public class InputSystemTests : InputTestFixture
    {
        class MockClusterSync : IClusterSyncState
        {
            public string InstanceName { get; } = "MockClusterSync";
            public NodeRole NodeRole { get; set; }
            public bool EmitterIsHeadless { get; set; }
            public bool IsClusterLogicEnabled { get; set; }
            public bool IsTerminated { get; set; }
            public ulong Frame { get; set; }
            public byte NodeID { get; set; }
            public byte RenderNodeID { get; set; }
            public bool RepeatersDelayedOneFrame { get; set; }
            public bool ReplaceHeadlessEmitter { get; set; }

            public string GetDiagnostics()
            {
                throw new System.NotImplementedException();
            }
        }

        EmitterStateWriter m_EmitterStateWriter;
        TestUdpAgent m_EmitterAgent;
        TestUdpAgent m_RepeaterAgent;
        GameObject m_TestObject;

        [SetUp]
        public override void Setup()
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            m_EmitterStateWriter = new EmitterStateWriter(false);
            var testNetwork = new TestUdpAgentNetwork();

            m_EmitterAgent = new TestUdpAgent(testNetwork, EmitterNode.ReceiveMessageTypes.ToArray());
            m_RepeaterAgent = new TestUdpAgent(testNetwork, RepeaterNode.ReceiveMessageTypes.ToArray());
            m_TestObject = new GameObject("InputReplicator");
            base.Setup();
        }

        public override void TearDown()
        {
            RepeaterStateReader.ClearOnLoadDataDelegates();
            EmitterStateWriter.ClearCustomDataDelegates();
            m_EmitterStateWriter.Dispose();

            InputSystem.Update();

            // It is necessary to trigger InputSystemReplicator.OnDisable() before
            // InputTestFixture.TearDown() because InputTestFixture.TearDown() will remove all input devices, including
            // the virtual devices created by InputSystemReplicator. Otherwise, we'll be trying to remove the virtual
            // devices twice, resulting in an error.
            Object.Destroy(m_TestObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TestEmitterBroadcastsInputs()
        {
            ulong frameId = 0;

            // Pretend we're an emitter if anyone asks
            ServiceLocator.Provide<IClusterSyncState>(new MockClusterSync { NodeRole = NodeRole.Emitter });

            // Set up dummy UDP networking
            var frameSplitter = new FrameDataSplitter(m_EmitterAgent);
            using var frameAssembler = new FrameDataAssembler(m_RepeaterAgent, false);

            // The component under test
            var replicator = m_TestObject.AddComponent<InputSystemReplicator>();

            // Simulate some inputs
            var gamepad = InputSystem.AddDevice<Gamepad>();
            Set(gamepad.leftStick, new Vector2(0.123f, 0.234f));
            Set(gamepad.leftTrigger, 0.5f);

            ++frameId;

            // Advance a frame - allow InputSystemReplicator component to Update()
            yield return null;
            InputSystem.Update();

            // Simulate the frame state transmission
            // The InputSystemReplicator should have added the input data into the frame state at this point.
            m_EmitterStateWriter.GatherFrameState();
            m_EmitterStateWriter.PublishCurrentState(frameId, frameSplitter);

            // We should receive a framedata packet
            var message = m_RepeaterAgent.TryConsumeNextReceivedMessage() as ReceivedMessage<FrameData>;
            Assert.IsNotNull(message);
            Assert.That(message.Payload.FrameIndex, Is.EqualTo(frameId));

            // Check that the broadcasted input data is correct by simulating some repeater logic.
            // Use InputEventTrace to deserialize the input data
            using var eventTrace = new InputEventTrace();
            RepeaterStateReader.RegisterOnLoadDataDelegate((int)StateID.InputSystem, data =>
            {
                using var receiveStream = new MemoryStream();
                receiveStream.Write(data.AsReadOnlySpan());
                receiveStream.Flush();
                receiveStream.Position = 0;
                eventTrace.ReadFrom(receiveStream);
                return true;
            });
            Assert.That(eventTrace.eventCount, Is.Zero);
            RepeaterStateReader.RestoreEmitterFrameData(message.ExtraData.AsNativeArray());

            // Check the contents of the input data
            Assert.That(eventTrace.eventCount, Is.EqualTo(2));
            var currentEventPtr = default(InputEventPtr);
            Assert.IsTrue(eventTrace.GetNextEvent(ref currentEventPtr));
            Assert.That(gamepad.leftStick.ReadUnprocessedValueFromEvent(currentEventPtr), Is.EqualTo(new Vector2(0.123f, 0.234f)));
            Assert.IsTrue(eventTrace.GetNextEvent(ref currentEventPtr));
            Assert.That(gamepad.leftTrigger.ReadUnprocessedValueFromEvent(currentEventPtr), Is.EqualTo(0.5f));
        }

        NativeArray<byte> DumpEventTraceToFrameData(InputEventTrace trace)
        {
            using var inputStream = new MemoryStream();

            // On the emitter send, we serialize the input events
            trace.WriteTo(inputStream);
            inputStream.Flush();
            inputStream.Position = 0;
            trace.Clear();

            // Test repeater logic (just the part that responds to framedata)
            using FrameDataBuffer frameData = new();
            frameData.Store((int)StateID.InputSystem, buffer => inputStream.Read(buffer));
            inputStream.SetLength(0);

            var frameDataCopy = new NativeArray<byte>(frameData.Length, Allocator.Persistent);
            frameData.CopyTo(frameDataCopy);
            return frameDataCopy;
        }

        [UnityTest]
        public IEnumerator TestRepeaterReceivesInputs()
        {
            // Pretend we're a repeater if anyone asks.
            ServiceLocator.Provide<IClusterSyncState>(new MockClusterSync { NodeRole = NodeRole.Repeater });

            using var frameAssembler = new FrameDataAssembler(m_RepeaterAgent, false);

            var replicator = m_TestObject.AddComponent<InputSystemReplicator>();

            // Set up some test bindings
            var buttonAction = new InputAction(binding: "<Gamepad>/buttonEast", interactions: "Hold");
            var triggerAction = new InputAction(binding: "<Gamepad>/leftTrigger");
            var stickAction = new InputAction(binding: "<Gamepad>/leftStick");
            buttonAction.Enable();
            stickAction.Enable();
            triggerAction.Enable();

            // Capture some input data to test with
            var gamepad = InputSystem.AddDevice<Gamepad>();
            Assert.That(gamepad, Is.Not.Null);
            using var eventTrace = new InputEventTrace();
            eventTrace.Enable();
            InputSystem.EnableDevice(gamepad);
            Set(gamepad.leftStick, new Vector2(0.123f, 0.234f));
            Set(gamepad.leftTrigger, 0.5f);
            InputSystem.Update();
            InputSystem.DisableDevice(gamepad);
            Assert.That(eventTrace.eventCount, Is.EqualTo(2));
            using var frameData = DumpEventTraceToFrameData(eventTrace);

            var stickMoved = false;
            stickAction.performed += context =>
            {
                stickMoved = true;
                Assert.That(context.ReadValue<Vector2>(), Is.Not.EqualTo(Vector2.zero));
            };

            var triggerPressed = false;
            triggerAction.performed += context =>
            {
                triggerPressed = true;
                Assert.That(context.ReadValue<float>(), Is.Not.Zero);
            };

            // No events generated this frame so far
            Assert.IsFalse(stickMoved);
            Assert.IsFalse(triggerPressed);

            // Load the events into the InputSystemReplicator and it should play them back
            RepeaterStateReader.RestoreEmitterFrameData(frameData);

            InputSystem.Update();
            Assert.IsTrue(stickMoved);
            Assert.IsTrue(triggerPressed);
            eventTrace.Clear();

            yield return null;

            InputSystem.Update();
            // Record some more inputs
            InputSystem.EnableDevice(gamepad);
            Press(gamepad.buttonEast);
            InputSystem.Update();
            InputSystem.DisableDevice(gamepad);
            Assert.That(eventTrace.eventCount, Is.EqualTo(1));
            using var frameData2 = DumpEventTraceToFrameData(eventTrace);

            yield return null;

            var buttonPressed = false;
            buttonAction.started += _ => buttonPressed = true;

            InputSystem.Update();
            triggerPressed = false;
            stickMoved = false;
            // No events generated this frame so far
            Assert.IsFalse(buttonPressed);

            // Assert that events can be played back.
            // Should not play back old events.
            RepeaterStateReader.RestoreEmitterFrameData(frameData2);

            InputSystem.Update();
            Assert.IsTrue(buttonPressed);
            Assert.IsFalse(stickMoved);
            Assert.IsFalse(triggerPressed);
        }
    }
}

#endif
