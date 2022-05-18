using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Unity.ClusterDisplay
{
    abstract class NodeState
    {
        protected Stopwatch m_Time;
        public NodeState PendingStateChange { get; set; }
        public static bool Debugging { get; set; }

        protected TimeSpan MaxTimeOut { get; set; } = new(0, 0, 0, 30, 0);

        protected ClusterNode LocalNode { get; }

        protected NodeState(ClusterNode localNode) => LocalNode = localNode;

        // We want each deriving class to implement these so we stay
        // within or exit DoFrame or DoLateFrame safetly.
        public abstract bool ReadyToProceed { get; }
        public abstract bool ReadyForNextFrame { get; }

        public NodeState EnterState(NodeState oldState)
        {
            if (oldState != this)
            {
                if (oldState != null)
                {
                    oldState.ExitState();
                }
                m_Time = new Stopwatch();
                m_Time.Start();

                InitState();
            }

            return this;
        }

        protected virtual void InitState()
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
                    var shutdown = new Shutdown();
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
                    PendingStateChange = new Shutdown();
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

    /// <summary>
    /// Type-safe variant of <see cref="NodeState"/>
    /// </summary>
    /// <typeparam name="T">The local node type</typeparam>
    abstract class NodeState<T> : NodeState where T : ClusterNode
    {
        protected new T LocalNode => base.LocalNode as T;
        protected NodeState(T node) : base(node)
        {
        }
    }

    // Shutdown state --------------------------------------------------------
    sealed class Shutdown : NodeState
    {
        public override bool ReadyToProceed => true;
        public override bool ReadyForNextFrame => true;

        public Shutdown()
            : base(null) { }
    }

    // FatalError state --------------------------------------------------------
    sealed class FatalError : NodeState
    {
        public override bool ReadyToProceed => true;
        public override bool ReadyForNextFrame => true;

        public FatalError(string msg)
            : base(null)
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
