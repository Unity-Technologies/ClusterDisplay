using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// ConditionVariable pattern for <see cref="ValueTask"/>s.
    /// </summary>
    /// <remarks>Avoid heap allocation but not as flexible as <see cref="AsyncConditionVariable"/>.  Only one thread
    /// should call <see cref="SignaledValueTask"/> and only one thread (can be another one) should call the
    /// <see cref="Signal"/> method.</remarks>
    public class AsyncConditionVariableValueTask
    {
        /// <summary>
        /// A <see cref="ValueTask"/> that is completed once the <see cref="AsyncConditionVariableValueTask"/> is
        /// signaled.
        /// </summary>
        /// <remarks>Ideally every <see cref="ValueTask"/> returned by this method should be awaited on or else the
        /// class will not be truly heap allocation free.</remarks>
        public ValueTask SignaledValueTask
        {
            get
            {
                ValueTaskSource valueTaskSource;
                bool cancelValueTaskSource;
                lock (m_Lock)
                {
                    valueTaskSource = GetFreeValueTaskSource();
                    var previousValueTaskSource = Interlocked.CompareExchange(ref m_ToBeSignaled, valueTaskSource, null);
                    if (previousValueTaskSource != null)
                    {
                        // Looks like SignaledValueTask was called twice and the previous one never awaited on...  Not ideal but
                        // we can still continue, let's use the same one as last time...
                        ReturnValueTaskSource(valueTaskSource);
                        valueTaskSource = previousValueTaskSource;
                    }

                    cancelValueTaskSource = m_IsCanceled;
                    if (cancelValueTaskSource)
                    {
                        m_ToBeSignaled = null;
                    }
                }

                if (cancelValueTaskSource)
                {
                    valueTaskSource.Signal(ValueTaskSourceStatus.Canceled);
                }

                return new ValueTask(valueTaskSource, valueTaskSource.Token);
            }
        }

        /// <summary>
        /// Signal the <see cref="AsyncConditionVariableValueTask"/> so that any code waiting on the
        /// <see cref="ValueTask"/> returned by <see cref="SignaledValueTask"/> is executed.
        /// </summary>
        public void Signal()
        {
            ValueTaskSource toSignal;
            lock (m_Lock)
            {
                // Just do nothing if we are canceled (anyone asking for the task will anyway receive an exception).
                if (m_IsCanceled)
                {
                    return;
                }

                toSignal = Interlocked.Exchange(ref m_ToBeSignaled, null);
                // else, no one waiting, so nothing special to signal...
            }

            toSignal?.Signal();
        }

        /// <summary>
        /// Set the state of the last <see cref="ValueTask"/> returned by <see cref="SignaledValueTask"/> to canceled.
        /// </summary>
        public void Cancel()
        {
            ValueTaskSource toSignal;
            lock (m_Lock)
            {
                m_IsCanceled = true;

                toSignal = Interlocked.Exchange(ref m_ToBeSignaled, null);
                // else, no one waiting, so nothing special to signal...
            }

            toSignal?.Signal(ValueTaskSourceStatus.Canceled);
        }

        /// <summary>
        /// <see cref="IValueTaskSource"/>'s implementation for <see cref="AsyncConditionVariableValueTask"/>.
        /// </summary>
        /// <remarks>Implementing <see cref="IValueTaskSource"/> can be tricky, here are a few references that might
        /// help to understand what is going in here: <br/>
        /// https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/<br/>
        /// https://tooslowexception.com/implementing-custom-ivaluetasksource-async-without-allocations/</remarks>
        class ValueTaskSource : IValueTaskSource
        {
            public void GetResult(short token)
            {
                if (token != m_Token)
                {
                    ThrowUnexpectedToken();
                }

                bool isCanceled = m_Status == ValueTaskSourceStatus.Canceled;

                ReturnValueTaskSource(this);

                if (isCanceled)
                {
                    throw new OperationCanceledException();
                }
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                if (token != m_Token)
                {
                    ThrowUnexpectedToken();
                }

                return m_Status;
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                if (token != m_Token)
                {
                    ThrowUnexpectedToken();
                }

                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
                {
                    m_ExecutionContext = ExecutionContext.Capture();
                }

                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
                {
                    SynchronizationContext sc = SynchronizationContext.Current;
                    if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                    {
                        m_Scheduler = sc;
                    }
                    else
                    {
                        TaskScheduler ts = TaskScheduler.Current;
                        if (ts != TaskScheduler.Default)
                        {
                            m_Scheduler = ts;
                        }
                    }
                }

                // Remember current state
                m_State = state;

                // Remember continuation to be executed on completed (if not already completed, in case of which
                // continuation will be set to k_CallbackCompleted)
                var previousContinuation = Interlocked.CompareExchange(ref m_Continuation, continuation, null);
                if (previousContinuation != null)
                {
                    if (!ReferenceEquals(previousContinuation, k_CallbackCompleted))
                    {
                        throw new InvalidOperationException("Trying to replace a m_Continuation that was k_CallbackCompleted");
                    }

                    // Lost the race condition and the operation has now already completed.  We need to invoke the
                    // continuation, but it must be asynchronously to avoid a stack dive.  However, since all of the
                    // queueing mechanisms flow ExecutionContext, and since we're still in the same context where we
                    // captured it, we can just ignore the one we captured.
                    m_ExecutionContext = null;
                    m_State = null; // we have the state in "state"; no need for the one in UserToken
                    InvokeContinuation(continuation, state, forceAsync: true);
                }
            }

            /// <summary>
            /// Call continuation.
            /// </summary>
            public void Signal(ValueTaskSourceStatus status = ValueTaskSourceStatus.Succeeded)
            {
                m_Status = status;
                var previousContinuation = Interlocked.CompareExchange(ref m_Continuation, k_CallbackCompleted, null);
                if (previousContinuation != null)
                {
                    // Async work completed, continue with... continuation
                    ExecutionContext executionContext = m_ExecutionContext;
                    if (executionContext == null)
                    {
                        InvokeContinuation(previousContinuation, m_State, forceAsync: false);
                    }
                    else
                    {
                        // This case should be relatively rare, as the async Task/ValueTask method builders use the
                        // awaiter's UnsafeOnCompleted, so this will only happen with code that explicitly uses the
                        // awaiter's OnCompleted instead.
                        m_ExecutionContext = null;
                        ExecutionContext.Run(executionContext, runState =>
                        {
                            var t = (Tuple<ValueTaskSource, Action<object>, object>)runState;
                            t.Item1.InvokeContinuation(t.Item2, t.Item3, forceAsync: false);
                        }, Tuple.Create(this, previousContinuation, this.m_State));
                    }
                }
            }

            /// <summary>
            /// Reference next free <see cref="ValueTaskSource"/> to be reused.
            /// </summary>
            public ValueTaskSource NextFree { get; set; }

            /// <summary>
            /// Current token value of ValueTaskSource.
            /// </summary>
            public short Token => m_Token;

            /// <summary>
            /// Increase current token of the <see cref="ValueTaskSource"/> (should be called between each usage of a
            /// <see cref="ValueTaskSource"/>).
            /// </summary>
            public void Recycle()
            {
                ++m_Token;
                m_Status = ValueTaskSourceStatus.Pending;
                m_State = null;
                m_Continuation = null;
            }

            /// <summary>
            /// Throw an exception when token mismatch detected.
            /// </summary>
            /// <exception cref="InvalidOperationException"></exception>
            static void ThrowUnexpectedToken()
            {
                throw new InvalidOperationException($"Invalid token, verify {nameof(AsyncConditionVariableValueTask)} " +
                    $"usage");
            }

            void InvokeContinuation(Action<object> continuation, object state, bool forceAsync)
            {
                if (continuation == null)
                {
                    return;
                }

                var scheduler = m_Scheduler;
                m_Scheduler = null;
                if (scheduler != null)
                {
                    if (scheduler is SynchronizationContext sc)
                    {
                        sc.Post(s =>
                        {
                            var t = (Tuple<Action<object>, object>)s;
                            t.Item1(t.Item2);
                        }, Tuple.Create(continuation, state));
                    }
                    else
                    {
                        Debug.Assert(scheduler is TaskScheduler, $"Expected TaskScheduler, got {scheduler}");
                        Task.Factory.StartNew(continuation, state, CancellationToken.None,
                            TaskCreationOptions.DenyChildAttach, (TaskScheduler)scheduler);
                    }
                }
                else if (forceAsync)
                {
                    ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: true);
                }
                else
                {
                    continuation(state);
                }
            }

            /// <summary>
            /// Token used to detect miss-usage of <see cref="ValueTaskSource"/>.
            /// </summary>
            short m_Token;
            /// <summary>
            /// Status of the ValueTaskSource
            /// </summary>
            ValueTaskSourceStatus m_Status;

            /// <summary>
            /// Action to call to continue execution.
            /// </summary>
            Action<object> m_Continuation;
            /// <summary>
            /// Save state (to pass to <see cref="m_Continuation"/> when calling it).
            /// </summary>
            object m_State;

            ExecutionContext m_ExecutionContext;
            object m_Scheduler;

            static readonly Action<object> k_CallbackCompleted = _ => { Debug.Assert(false, "Should not be invoked"); };
        }

        /// <summary>
        /// Gets a re-used or brand new <see cref="ValueTaskSource"/>
        /// </summary>
        static ValueTaskSource GetFreeValueTaskSource()
        {
            for (;;)
            {
                var ret = s_FreeValueTaskSources;
                if (ret == null)
                {
                    return new();
                }

                if (Interlocked.CompareExchange(ref s_FreeValueTaskSources, ret.NextFree, ret) == ret)
                {
                    ret.NextFree = null;
                    return ret;
                }
            }
        }

        /// <summary>
        /// Returns a ValueTaskSource returned by <see cref="GetFreeValueTaskSource"/> to a list of free
        /// <see cref="ValueTaskSource"/> to be re-used.
        /// </summary>
        static void ReturnValueTaskSource(ValueTaskSource toReturn)
        {
            toReturn.Recycle();
            for (;;)
            {
                toReturn.NextFree = s_FreeValueTaskSources;
                if (Interlocked.CompareExchange(ref s_FreeValueTaskSources, toReturn, toReturn.NextFree) ==
                    toReturn.NextFree)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Lock access to member variables below.
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// Indicate that the ConditionVariable was canceled.
        /// </summary>
        bool m_IsCanceled;

        /// <summary>
        /// <see cref="ValueTaskSource"/> used to return a <see cref="ValueTask"/> by <see cref="SignaledValueTask"/>,
        /// so the one to be marked as completed when signaling.
        /// </summary>
        ValueTaskSource m_ToBeSignaled;

        /// <summary>
        /// Chain of free <see cref="ValueTaskSource"/>.
        /// </summary>
        static ValueTaskSource s_FreeValueTaskSources;
    }
}
