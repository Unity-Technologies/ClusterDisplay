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
    }
}
