using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    public delegate bool AccumulateFrameDataDelegate(NativeArray<byte> buffer, ref int endPos);

    internal abstract class NodeState
    {
        protected Stopwatch m_Time;
        public NodeState PendingStateChange { get; set; }
        public static bool Debugging { get; set; }
        public TimeSpan MaxTimeOut = new(0, 0, 0, 30, 0);

        protected IClusterSyncState clusterSync;

        public NodeState(IClusterSyncState clusterSync) =>
            this.clusterSync = clusterSync;

        // We want each deriving class to implement these so we stay
        // within or exit DoFrame or DoLateFrame safetly.
        public abstract bool ReadyToProceed { get; }
        public abstract bool ReadyForNextFrame { get; }

        public NodeState EnterState(NodeState oldState)
        {
            if (oldState != this)
            {
                oldState?.ExitState();
                m_Time = new Stopwatch();
                m_Time.Start();

                InitState();
            }

            return this;
        }

        public virtual void InitState()
        {
        }

        protected virtual NodeState DoFrame(bool newFrame)
        {
            return this;
        }

        protected virtual void DoLateFrame() { }

        public virtual void OnEndFrame() { }

        public NodeState ProcessFrame(bool newFrame)
        {
            var res = DoFrame(newFrame);
            if (res != this)
            {
                res.EnterState(this);
                return res;
            }

            // RemoveMe
            if (Debugging)
            {
                if (newFrame)
                    m_Time.Restart();

                if (m_Time.ElapsedMilliseconds > MaxTimeOut.Milliseconds)
                {
                    var shutdown = new Shutdown(clusterSync);
                    return shutdown.EnterState(this);
                }
            }

            if (PendingStateChange != null)
                return PendingStateChange.EnterState(this);

            return this;
        }

        public void ProcessLateFrame() => DoLateFrame();

        protected void ProcessUnhandledMessage(MessageHeader msgHeader)
        {
            switch (msgHeader.MessageType)
            {
                case EMessageType.GlobalShutdownRequest:
                {
                    PendingStateChange = new Shutdown(clusterSync);
                    break;
                }

                case EMessageType.HelloEmitter:
                    break;

                case EMessageType.WelcomeRepeater:
                    break;

                default:
                {
                    throw new Exception($"Unexpected network message received: \"{msgHeader.MessageType}\".");
                }
            }
        }

        protected virtual void ExitState()
        {
            ClusterDebug.Log("Exiting State:" + GetType().Name);
        }

        public virtual string GetDebugString()
        {
            return GetType().Name;
        }
    }

    // RepeaterState state --------------------------------------------------------
    internal abstract class RepeaterState : NodeState
    {
        public RepeaterState(IClusterSyncState clusterSync)
            : base(clusterSync) { }

        protected ulong CurrentFrameID => clusterSync.CurrentFrameID;

        protected RepeaterNode LocalNode => (RepeaterNode) clusterSync.LocalNode;
    }

    // EmitterState state --------------------------------------------------------
    internal abstract class EmitterState : NodeState
    {
        public EmitterState(IClusterSyncState clusterSync)
            : base(clusterSync) { }

        // If the repeaters were delayed by one frame, then we need to send emitter's data from the
        // last frame. That last frame data is sent with this previous frame number for validation
        // by the repeater.
        protected ulong PreviousFrameID =>
            LocalNode.RepeatersDelayed ? CurrentFrameID - 1 : CurrentFrameID;

        protected ulong CurrentFrameID => clusterSync.CurrentFrameID;
        protected EmitterNode LocalNode => (EmitterNode) clusterSync.LocalNode;
    }

    // Shutdown state --------------------------------------------------------
    internal class Shutdown : NodeState
    {
        public override bool ReadyToProceed => true;
        public override bool ReadyForNextFrame => true;

        public Shutdown(IClusterSyncState clusterSync)
            : base(clusterSync) { }
    }

    // FatalError state --------------------------------------------------------
    internal class FatalError : NodeState
    {
        public override bool ReadyToProceed => true;
        public override bool ReadyForNextFrame => true;

        public FatalError(IClusterSyncState clusterSync, string msg)
            : base(clusterSync)
        {
            Message = msg;
        }

        protected override void ExitState()
        {
            base.ExitState();
            ClusterDebug.LogError(Message);
        }

        public string Message { get; }
    }
}
