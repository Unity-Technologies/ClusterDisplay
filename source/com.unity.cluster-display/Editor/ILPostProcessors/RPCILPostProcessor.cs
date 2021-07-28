using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mono.Cecil;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private IEnumerable<MethodDefinition> GetMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef, out string rpcMethodAttributeFullName)
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            var attributeFullName = rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;
            ConcurrentQueue<MethodDefinition> queuedMethodDefs = new ConcurrentQueue<MethodDefinition>();

            for (int moduleIndex = 0; moduleIndex < compiledAssemblyDef.Modules.Count; moduleIndex++)
            {
                int typeCount = compiledAssemblyDef.Modules[moduleIndex].Types.Count;

                int workerCount = Mathf.Min(Environment.ProcessorCount, typeCount);
                int typeCountPerWorker = typeCount / workerCount;
                int remainder = typeCount % workerCount;

                Parallel.For(0, workerCount, workerId =>
                {
                    int start = typeCountPerWorker * workerId;
                    int end = typeCountPerWorker * (workerId + 1);
                    if (workerId == Environment.ProcessorCount - 1)
                        end += remainder;

                    // Debug.Log($"Worker: {workerId}, Start: {start}, End: {end}, Type Count: {typeCount}");

                    var types = compiledAssemblyDef.Modules[moduleIndex].Types;
                    for (int typeIndex = start; typeIndex < end; typeIndex++)
                    {
                        var type = types[typeIndex];
                        if (type == null)
                            continue;

                        var methods = type.Methods;
                        for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
                        {
                            var method = methods[methodIndex];
                            if (method == null)
                                continue;

                            var customAttributes = method.CustomAttributes;
                            for (int caIndex = 0; caIndex < customAttributes.Count; caIndex++)
                            {
                                var customAttribute = customAttributes[caIndex];

                                if (customAttribute.AttributeType.FullName != attributeFullName)
                                {
                                    // If the custom attribute does not match, then check it's base type as this attribute may be an
                                    // obsolete version of the RPC attribute that derrives from the current one.
                                    var attributeType = customAttribute.AttributeType.Resolve();
                                    if (attributeType.BaseType.FullName != attributeFullName)
                                        continue;
                                }

                                queuedMethodDefs.Enqueue(method);
                            }
                        }
                    }
                });
            }

            return queuedMethodDefs.OrderBy(methodDef => methodDef.FullName);
        }

        private bool TryGetRPCILGenerators (
            AssemblyDefinition compiledAssemblyDef, 
            MethodDefinition targetMethodDef,
            out RPCILGenerator rpcILGenerator, 
            out QueuedRPCILGenerator queuedRPCILGenerator)
        {
            rpcILGenerator = null;
            queuedRPCILGenerator = null;

            if (targetMethodDef.IsStatic)
            {
                if (!TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out rpcILGenerator))
                    return false;
            }

            else if (!TryGetCachedRPCILGenerator(compiledAssemblyDef, out rpcILGenerator))
                return false;

            if (!TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out queuedRPCILGenerator))
                return false;

            return true;
        }

        private void InjectDefaultSwitchCases ()
        {
            if (cachedOnTryCallProcessor != null)
                cachedOnTryCallProcessor.InjectDefaultSwitchCase();

            if (cachedOnTryStaticCallProcessor != null)
                cachedOnTryStaticCallProcessor.InjectDefaultSwitchCase();

            if (cachedQueuedRPCILGenerator != null)
                cachedQueuedRPCILGenerator.InjectDefaultSwitchCase();
        }

        private Dictionary<uint, HashSet<uint>> processedMethods = new Dictionary<uint, HashSet<uint>>();
        private bool MethodAlreadyProcessed (MethodReference methodRef) => 
            processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash) &&
            hash.Contains(methodRef.MetadataToken.RID);

        private void AddProcessedMethod (MethodReference methodRef)
        {
            if (!processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash))
            {
                processedMethods.Add(methodRef.DeclaringType.MetadataToken.RID, new HashSet<uint>() { methodRef.MetadataToken.RID });
                return;
            }

            hash.Add(methodRef.MetadataToken.RID);
        }

        private bool TryProcessSerializedRPCs (
            AssemblyDefinition compiledAssemblyDef, 
            SerializedRPC[] serializedRPCs)
        {
            foreach (var serializedRPC in serializedRPCs)
            {
                var rpc = serializedRPC;

                // If the declaring assembly name does not match our compiled one, then ignore it as the RPC is probably in another assembly.
                if (rpc.method.declaringAssemblyName == compiledAssemblyDef.Name.Name)
                {
                    var typeDefinition = compiledAssemblyDef.MainModule.GetType(rpc.method.declaryingTypeFullName);
                    if (typeDefinition == null)
                    {
                        Debug.Log($"Unable to find serialized method: \"{rpc.method.methodName}\", the declaring type: \"{rpc.method.declaryingTypeFullName}\" does not exist in the compiled assembly: \"{compiledAssemblyDef.Name}\".");
                        continue;
                    }

                    MethodReference targetMethodRef = null;

                    bool unableToFindAnyMethod =
                        !TryFindMethodWithMatchingFormalySerializedAs(
                            compiledAssemblyDef.MainModule,
                            typeDefinition,
                            rpc.method.methodName,
                            out targetMethodRef) &&
                        !CecilUtils.TryGetMethodReference(compiledAssemblyDef.MainModule, typeDefinition, ref rpc, out targetMethodRef);

                    if (unableToFindAnyMethod)
                        continue;

                    if (MethodAlreadyProcessed(targetMethodRef))
                        continue;

                    if (!TryGetRPCILGenerators(compiledAssemblyDef, targetMethodRef.Resolve(), out var rpcILGenerator, out var queuedRPCILGenerator))
                        return false;

                    var executionStage = (RPCExecutionStage)serializedRPC.rpcExecutionStage;
                    if (!rpcILGenerator.ProcessMethodDef(
                        serializedRPC.rpcId,
                        targetMethodRef,
                        executionStage))
                        return false;

                    if (executionStage != RPCExecutionStage.ImmediatelyOnArrival)
                        if (!queuedRPCILGenerator.TryInjectILToExecuteQueuedRPC(targetMethodRef, executionStage, serializedRPC.rpcId))
                            return false;

                    AddProcessedMethod(targetMethodRef);
                }

                // Even if an RPC is in another assembly, we still want to store it's RPC ID to flag it's use.
                UniqueRPCIdManager.Add(rpc.rpcId);
            }

            return true;
        }

        private bool TryPollSerializedRPCs (AssemblyDefinition compiledAssemblyDef)
        {
            if (cachedSerializedRPCS == null)
                RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out cachedSerializedRPCS, out var serializedStagedMethods);

            if (cachedSerializedRPCS != null && cachedSerializedRPCS.Length > 0)
                if (!TryProcessSerializedRPCs(compiledAssemblyDef, cachedSerializedRPCS.ToArray()))
                    return false;

            UniqueRPCIdManager.PollUnused();
            return true;
        }

        private void SetRPCAttributeRPCIDArgument (CustomAttribute customAttribute, int rpcIdAttributeArgumentIndex, ushort newRPCId)
        {
            var customAttributeArgument = customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex];
            customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex] = new CustomAttributeArgument(customAttributeArgument.Type, newRPCId);
        }

        private bool TryProcessMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef)
        {
            var methodDefs = GetMethodsWithRPCAttribute(compiledAssemblyDef, out var rpcMethodAttributeFullName);
            foreach (var methodDef in methodDefs)
            {
                if (methodDef == null)
                    continue;

                if (MethodAlreadyProcessed(methodDef))
                    continue;

                var customAttribute = methodDef.CustomAttributes.First(ca => ca.AttributeType.FullName == rpcMethodAttributeFullName);
                if (!CecilUtils.TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<ClusterRPC.RPCExecutionStageMarker>(customAttribute, out var rpcExecutionStageAttributeArgumentIndex) ||
                    !CecilUtils.TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<ClusterRPC.RPCIDMarker>(customAttribute, out var rpcIdAttributeArgumentIndex))
                    return false;

                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[rpcExecutionStageAttributeArgumentIndex];

                ushort newRPCId = UniqueRPCIdManager.Get();
                SetRPCAttributeRPCIDArgument(customAttribute, rpcIdAttributeArgumentIndex, newRPCId);

                RPCExecutionStage executionStage = (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value;
                if (!TryGetRPCILGenerators(compiledAssemblyDef, methodDef, out var rpcILGenerator, out var queuedRPCILGenerator))
                    return false;

                if (!rpcILGenerator.ProcessMethodDef(
                    newRPCId,
                    methodDef,
                    executionStage))
                    return false;

                if (executionStage != RPCExecutionStage.ImmediatelyOnArrival)
                    if (!queuedRPCILGenerator.TryInjectILToExecuteQueuedRPC(methodDef, executionStage, newRPCId))
                        return false;

                AddProcessedMethod(methodDef);
            }

            return true;
        }

        public bool TryProcess(AssemblyDefinition compiledAssemblyDef)
        {
            UniqueRPCIdManager.Read();
            if (!TryPollSerializedRPCs(compiledAssemblyDef))
                return false;

            if (!TryProcessMethodsWithRPCAttribute(compiledAssemblyDef))
                return false;

            InjectDefaultSwitchCases();
            FlushCache();
            UniqueRPCIdManager.Close();
            return true;
        }
    }
}
