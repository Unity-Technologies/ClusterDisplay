using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCTestRecorder
    {
        readonly Dictionary<string, bool> m_PropagatedRPCs = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> m_ExecutedRPCs = new Dictionary<string, bool>();

        public bool PropagatedAllRPCs
        {
            get
            {
                string rpcsToBePropagated = "Still waiting for propagation of the following RPCs:";
                bool allPropagated = true;
                foreach (var keyValuePair in m_PropagatedRPCs)
                {
                    if (keyValuePair.Value)
                    {
                        continue;
                    }

                    rpcsToBePropagated += $"\n\t\"{keyValuePair.Key}\"";
                    allPropagated = false;
                }

                if (!allPropagated)
                {
                    UnityEngine.Debug.LogWarning(rpcsToBePropagated);
                }

                return allPropagated;
            }
        }

        public bool IsTestFinished
        {
            get
            {
                string rpcsToBeExecuted = "Still waiting for execution of the following RPCs:";
                bool allExecuted = true;
                foreach (var keyValuePair in m_ExecutedRPCs)
                {
                    if (keyValuePair.Value)
                    {
                        continue;
                    }

                    rpcsToBeExecuted += $"\n\t\"{keyValuePair.Key}\"";
                    allExecuted = false;
                }

                if (!allExecuted)
                {
                    UnityEngine.Debug.LogWarning(rpcsToBeExecuted);
                }

                return allExecuted;
            }
        }

        public RPCTestRecorder (IRPCTestRecorder rpcTestRecorder)
        {
            var type = rpcTestRecorder.GetType();
            var bindingFlags =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance;

            var rpcMethods = type
                .GetMethods(bindingFlags)
                .Where(method => method.GetCustomAttribute<ClusterRPC>() != null)
                .ToArray();

            foreach (var rpcMethod in rpcMethods)
            {
                if (m_PropagatedRPCs.ContainsKey(rpcMethod.Name) || m_ExecutedRPCs.ContainsKey(rpcMethod.Name))
                {
                    throw new System.InvalidOperationException($"Cannot register multiple RPCs with the same method name: \"{rpcMethod.Name}\".");
                }

                m_PropagatedRPCs.Add(rpcMethod.Name, false);
                m_ExecutedRPCs.Add(rpcMethod.Name, false);
            }
        }

        public void RecordPropagation()
        {
            var method = FindClusterRPCMethod();
            if (!m_PropagatedRPCs.ContainsKey(method.Name))
            {
                throw new System.NullReferenceException($"Cannot record propagation of RPC: \"{method.Name}\", it was not registered.");
            }

            UnityEngine.Debug.Log($"RPC: \"{method.Name}\" was propagated.");
            m_PropagatedRPCs[method.Name] = true;
        }

        public void RecordExecution()
        {
            var method = FindClusterRPCMethod();
            if (!m_ExecutedRPCs.ContainsKey(method.Name))
            {
                throw new System.NullReferenceException($"Cannot record execution of RPC: \"{method.Name}\", it was not registered.");
            }

            UnityEngine.Debug.Log($"RPC: \"{method.Name}\" was executed.");
            m_ExecutedRPCs[method.Name] = true;
        }

        static MethodBase FindClusterRPCMethod()
        {
            var stackTrace = new StackTrace();

            int stackFrameIndex = 0;
            var stackFrame = stackTrace.GetFrame(stackFrameIndex++);

            MethodBase rpcMethod = null;
            while (stackFrame != null)
            {
                rpcMethod = stackFrame.GetMethod();
                if (rpcMethod.GetCustomAttribute<ClusterRPC>() != null)
                {
                    return rpcMethod;
                }

                stackFrame = stackTrace.GetFrame(stackFrameIndex++);
            }

            throw new System.InvalidOperationException($"Cannot find executed RPC in stack trace.");
        }
    }
}
