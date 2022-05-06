using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    class NodeStateWithCarriedPreDoFrame : NodeState
    {
        public NodeStateWithCarriedPreDoFrame(ClusterNode localNode)
            : base(localNode)
        {
            Assert.IsNull(CarriedPreDoFrameWork);
        }

        public override bool ReadyToProceed { get; }
        public override bool ReadyForNextFrame { get; }

        public void AddCarriedPreDoFrame(Func<bool> work)
        {
            if (CarriedPreDoFrameWork == null)
            {
                CarriedPreDoFrameWork = new();
            }
            CarriedPreDoFrameWork.Add(work);
        }
        public int CarriedPreDoFrameCount { get => ((CarriedPreDoFrameWork != null) ? CarriedPreDoFrameWork.Count : 0); }
        public bool CarriedPreDoFrameListAllocated { get => CarriedPreDoFrameWork != null;  }
    }

    public class NoteStateTests
    {
        RepeaterNode m_Node;

        [SetUp]
        public void SetUp()
        {
            var repeaterNodeConfig = new RepeaterNode.Config();
            repeaterNodeConfig.MainConfig.UdpAgentConfig = udpConfig;
            m_Node = new RepeaterNode(repeaterNodeConfig);
        }

        [Test]
        public void CarriedPreDoFrameExecute()
        {
            var testState = new NodeStateWithCarriedPreDoFrame(m_Node);

            testState.ProcessFrame(false);

            bool funcAExecuted = false;
            testState.AddCarriedPreDoFrame(() => { funcAExecuted = true; return true; });
            Assert.IsFalse(funcAExecuted);
            testState.ProcessFrame(false);
            Assert.IsTrue(funcAExecuted);

            funcAExecuted = false;
            bool funcBExecuted = false;
            testState.AddCarriedPreDoFrame(() => { funcBExecuted = true; return true; });
            Assert.IsFalse(funcAExecuted);
            Assert.IsFalse(funcBExecuted);
            testState.ProcessFrame(false);
            Assert.IsTrue(funcAExecuted);
            Assert.IsTrue(funcBExecuted);
        }

        [Test]
        public void CarriedPreDoFrameRemoveWhenDone()
        {
            var testState = new NodeStateWithCarriedPreDoFrame(m_Node);

            testState.ProcessFrame(false);

            int leftToExecuteA = 3;
            int leftToExecuteB = 2;
            int leftToExecuteC = 3;
            int leftToExecuteD = 3;
            int leftToExecuteE = 4;
            testState.AddCarriedPreDoFrame(() => { --leftToExecuteA; return leftToExecuteA > 0; });
            testState.AddCarriedPreDoFrame(() => { --leftToExecuteB; return leftToExecuteB > 0; });
            testState.AddCarriedPreDoFrame(() => { --leftToExecuteC; return leftToExecuteC > 0; });
            testState.AddCarriedPreDoFrame(() => { --leftToExecuteD; return leftToExecuteD > 0; });
            testState.AddCarriedPreDoFrame(() => { --leftToExecuteE; return leftToExecuteE > 0; });

            Assert.That(testState.CarriedPreDoFrameCount, Is.EqualTo(5));
            Assert.That(leftToExecuteA, Is.EqualTo(3));
            Assert.That(leftToExecuteB, Is.EqualTo(2));
            Assert.That(leftToExecuteC, Is.EqualTo(3));
            Assert.That(leftToExecuteD, Is.EqualTo(3));
            Assert.That(leftToExecuteE, Is.EqualTo(4));

            testState.ProcessFrame(false);
            Assert.That(testState.CarriedPreDoFrameCount, Is.EqualTo(5));
            Assert.That(leftToExecuteA, Is.EqualTo(2));
            Assert.That(leftToExecuteB, Is.EqualTo(1));
            Assert.That(leftToExecuteC, Is.EqualTo(2));
            Assert.That(leftToExecuteD, Is.EqualTo(2));
            Assert.That(leftToExecuteE, Is.EqualTo(3));

            testState.ProcessFrame(false);
            Assert.That(testState.CarriedPreDoFrameCount, Is.EqualTo(4));
            Assert.That(leftToExecuteA, Is.EqualTo(1));
            Assert.That(leftToExecuteB, Is.EqualTo(0));
            Assert.That(leftToExecuteC, Is.EqualTo(1));
            Assert.That(leftToExecuteD, Is.EqualTo(1));
            Assert.That(leftToExecuteE, Is.EqualTo(2));

            testState.ProcessFrame(false);
            Assert.That(testState.CarriedPreDoFrameCount, Is.EqualTo(1));
            Assert.That(leftToExecuteA, Is.EqualTo(0));
            Assert.That(leftToExecuteB, Is.EqualTo(0));
            Assert.That(leftToExecuteC, Is.EqualTo(0));
            Assert.That(leftToExecuteD, Is.EqualTo(0));
            Assert.That(leftToExecuteE, Is.EqualTo(1));

            testState.ProcessFrame(false);
            Assert.That(testState.CarriedPreDoFrameCount, Is.EqualTo(0));
            Assert.That(testState.CarriedPreDoFrameListAllocated, Is.False);
            Assert.That(leftToExecuteA, Is.EqualTo(0));
            Assert.That(leftToExecuteB, Is.EqualTo(0));
            Assert.That(leftToExecuteC, Is.EqualTo(0));
            Assert.That(leftToExecuteD, Is.EqualTo(0));
            Assert.That(leftToExecuteE, Is.EqualTo(0));
        }

        [Test]
        public void CarriedPreDoFrameEmptyToEmpty()
        {
            var testStateFrom = new NodeStateWithCarriedPreDoFrame(m_Node);
            var testStateTo = new NodeStateWithCarriedPreDoFrame(m_Node);
            testStateTo.EnterState(testStateFrom);
            Assert.That(testStateTo.CarriedPreDoFrameCount, Is.EqualTo(0));
            Assert.That(testStateTo.CarriedPreDoFrameListAllocated, Is.False);
        }

        [Test]
        public void CarriedPreDoFrameEmptyToNotEmpty()
        {
            var testStateFrom = new NodeStateWithCarriedPreDoFrame(m_Node);
            var testStateTo = new NodeStateWithCarriedPreDoFrame(m_Node);
            int testStateToExecuted = 0;
            testStateTo.AddCarriedPreDoFrame(() => { ++testStateToExecuted; return true; });
            testStateTo.EnterState(testStateFrom);
            Assert.That(testStateTo.CarriedPreDoFrameCount, Is.EqualTo(1));
            Assert.That(testStateTo.CarriedPreDoFrameListAllocated, Is.True);

            testStateTo.ProcessFrame(false);
            Assert.That(testStateToExecuted, Is.EqualTo(1));
        }

        [Test]
        public void CarriedPreDoFrameNotEmptyToEmpty()
        {
            var testStateFrom = new NodeStateWithCarriedPreDoFrame(m_Node);
            int testStateFromExecuted = 0;
            testStateFrom.AddCarriedPreDoFrame(() => { ++testStateFromExecuted; return true; });
            var testStateTo = new NodeStateWithCarriedPreDoFrame(m_Node);
            testStateTo.EnterState(testStateFrom);
            Assert.That(testStateTo.CarriedPreDoFrameCount, Is.EqualTo(1));
            Assert.That(testStateTo.CarriedPreDoFrameListAllocated, Is.True);

            testStateTo.ProcessFrame(false);
            Assert.That(testStateFromExecuted, Is.EqualTo(1));
        }

        [Test]
        public void CarriedPreDoFrameNotEmptyToNotEmpty()
        {
            var testStateFrom = new NodeStateWithCarriedPreDoFrame(m_Node);
            int testStateFromExecuted = 0;
            testStateFrom.AddCarriedPreDoFrame(() => { ++testStateFromExecuted; return true; });
            var testStateTo = new NodeStateWithCarriedPreDoFrame(m_Node);
            int testStateToExecuted = 0;
            testStateTo.AddCarriedPreDoFrame(() => { ++testStateToExecuted; return true; });
            testStateTo.EnterState(testStateFrom);
            Assert.That(testStateTo.CarriedPreDoFrameCount, Is.EqualTo(2));
            Assert.That(testStateTo.CarriedPreDoFrameListAllocated, Is.True);

            testStateTo.ProcessFrame(false);
            Assert.That(testStateFromExecuted, Is.EqualTo(1));
            Assert.That(testStateToExecuted, Is.EqualTo(1));
        }
    }
}
