#if ENABLE_INPUT_SYSTEM
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Scripting;
using Unity.ClusterDisplay.Tests;
using Unity.ClusterDisplay.Utils;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class InputSystemTests : InputTestFixture
{
    class MockClusterSync : IClusterSyncState
    {
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

    public override void Setup()
    {
        base.Setup();

        m_EmitterStateWriter = new EmitterStateWriter(false);
        var testNetwork = new TestUdpAgentNetwork();

        m_EmitterAgent = new TestUdpAgent(testNetwork, EmitterNode.ReceiveMessageTypes.ToArray());
        m_RepeaterAgent = new TestUdpAgent(testNetwork, RepeaterNode.ReceiveMessageTypes.ToArray());
    }

    public override void TearDown()
    {
        RepeaterStateReader.ClearOnLoadDataDelegates();
        EmitterStateWriter.ClearCustomDataDelegates();
        m_EmitterStateWriter.Dispose();
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator TestEmitterBroadcastsInputs()
    {
        ulong frameId = 0;

        // Pretend we're an emitter
        ServiceLocator.Provide<IClusterSyncState>(new MockClusterSync { NodeRole = NodeRole.Emitter });

        // Set up dummy UDP networking
        var frameSplitter = new FrameDataSplitter(m_EmitterAgent);
        using var frameAssembler = new FrameDataAssembler(m_RepeaterAgent, false);
        var testGameObject = new GameObject();

        // The component under test
        var replicator = testGameObject.AddComponent<InputSystemReplicator>();

        // Simulate some inputs
        var gamepad = InputSystem.AddDevice<Gamepad>();

        // TODO: Investigate possible serialization bug
        // Press(gamepad.buttonEast);

        Set(gamepad.leftStick, new Vector2(0.123f, 0.234f));
        Set(gamepad.leftTrigger, 0.5f);

        ++frameId;

        // Advance a frame - allow InputSystemReplicator component to Update()
        yield return null;

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
        RepeaterStateReader.RestoreEmitterFrameData(message.ExtraData.AsNativeArray());

        // Check the contents of the input data
        Assert.That(eventTrace.eventCount, Is.EqualTo(2));
        var currentEventPtr = default(InputEventPtr);
        Assert.IsTrue(eventTrace.GetNextEvent(ref currentEventPtr));
        Assert.That(gamepad.leftStick.ReadUnprocessedValueFromEvent(currentEventPtr), Is.EqualTo(new Vector2(0.123f, 0.234f)));
        Assert.IsTrue(eventTrace.GetNextEvent(ref currentEventPtr));
        Assert.That(gamepad.leftTrigger.ReadUnprocessedValueFromEvent(currentEventPtr), Is.EqualTo(0.5f));
    }

    [Test]
    public void TestRepeaterReceivesInputs()
    {
        ulong frameId = 0;

        // Pretend we're a repeater
        ServiceLocator.Provide<IClusterSyncState>(new MockClusterSync { NodeRole = NodeRole.Repeater });

        using var frameAssembler = new FrameDataAssembler(m_RepeaterAgent, false);
        var testGameObject = new GameObject();
        var replicator = testGameObject.AddComponent<InputSystemReplicator>();

        // Capture some input data to test with
        using var eventTrace = new InputEventTrace();
        eventTrace.Enable();

        var gamepad = InputSystem.AddDevice<Gamepad>();
        Set(gamepad.leftStick, new Vector2(0.123f, 0.234f));
        Set(gamepad.leftTrigger, 0.5f);
        InputSystem.Update();

        // Store the input events in a FrameDataBuffer
        using var inputStream = new MemoryStream();
        eventTrace.WriteTo(inputStream);
        eventTrace.Disable();
        inputStream.Flush();
        inputStream.Position = 0;

        using var frameData = new FrameDataBuffer();
        frameData.Store((int)StateID.InputSystem, buffer => inputStream.Read(buffer));

        ++frameId;

        // Set up some test bindings
        var triggerAction = new InputAction(binding: "<Gamepad>/leftTrigger");
        var stickAction = new InputAction(binding: "<Gamepad>/leftStick");
        stickAction.Enable();
        triggerAction.Enable();

        // Test repeater logic (just the part that responds to framedata)
        using var frameDataCopy = new NativeArray<byte>(frameData.Length, Allocator.Persistent);
        frameData.CopyTo(frameDataCopy);
        RepeaterStateReader.RestoreEmitterFrameData(frameDataCopy);

        // Check that repeater performs the input actions
        // The replicator should have started playing back the deserialized input data at this point
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

        InputSystem.Update();

        stickAction.Disable();
        triggerAction.Disable();

        Assert.IsTrue(triggerPressed);
        Assert.IsTrue(stickMoved);
    }
}
#endif
