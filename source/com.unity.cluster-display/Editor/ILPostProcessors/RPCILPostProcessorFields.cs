using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public partial class RPCILPostProcessor : ILPostProcessor
    {
        private const string attributeSearchAssemblyName = "ILPostprocessorAttributes";
        private static TypeReference cachedStringTypeRef;
        private static MethodInfo cachedGetIsMasterMethod;
        private static MethodReference cachedDebugLogMethodRef;
        private static MethodReference cachedObjectRegistryGetItemMethodRef;

        private static readonly Dictionary<RPCExecutionStage, ILProcessor> cachedExecuteQueuedRPCMethodILProcessors = new Dictionary<RPCExecutionStage, ILProcessor>();
        private static readonly Dictionary<RPCExecutionStage, Instruction> executionStageLastSwitchJmpInstructions = new Dictionary<RPCExecutionStage, Instruction>();

        private static readonly Dictionary<MetadataToken, Call> cachedCallTree = new Dictionary<MetadataToken, Call>();

        private static readonly Dictionary<string, RPCExecutionStage> cachedMonoBehaviourMethodSignaturesForRPCExecutionStages = new Dictionary<string, RPCExecutionStage>();
    }
}
