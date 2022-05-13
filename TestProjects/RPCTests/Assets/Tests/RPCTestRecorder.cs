using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Tests
{
    public class RPCTestRecorder
    {
        readonly Dictionary<string, bool> propagatedRPCs = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> executedRPCs = new Dictionary<string, bool>();

        public bool PropagatedAllRPCs
        {
            get
            {
                string rpcsToBePropagated = "Still waiting for propagation of the following RPCs:";
                bool allPropagated = true;
                foreach (var keyValuePair in propagatedRPCs)
                {
                    if (keyValuePair.Value)
                    {
                        allPropagated &= true;
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
                foreach (var keyValuePair in executedRPCs)
                {
                    if (keyValuePair.Value)
                    {
                        allExecuted &= true;
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
                if (propagatedRPCs.ContainsKey(rpcMethod.Name) || executedRPCs.ContainsKey(rpcMethod.Name))
                {
                    throw new System.InvalidOperationException($"Cannot register multiple RPCs with the same method name: \"{rpcMethod.Name}\".");
                }

                propagatedRPCs.Add(rpcMethod.Name, false);
                executedRPCs.Add(rpcMethod.Name, false);
            }
        }

        public void RecordPropagation()
        {
            var method = FindClusterRPCMethod();
            if (!propagatedRPCs.ContainsKey(method.Name))
            {
                throw new System.NullReferenceException($"Cannot record propagation of RPC: \"{method.Name}\", it was not registered.");
            }

            UnityEngine.Debug.Log($"RPC: \"{method.Name}\" was propagated.");
            propagatedRPCs[method.Name] = true;
        }

        public void RecordExecution()
        {
            var method = FindClusterRPCMethod();
            if (!executedRPCs.ContainsKey(method.Name))
            {
                throw new System.NullReferenceException($"Cannot record execution of RPC: \"{method.Name}\", it was not registered.");
            }

            UnityEngine.Debug.Log($"RPC: \"{method.Name}\" was executed.");
            executedRPCs[method.Name] = true;
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
