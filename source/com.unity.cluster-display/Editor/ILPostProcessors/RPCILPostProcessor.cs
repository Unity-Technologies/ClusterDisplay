using System.Linq;
using UnityEngine;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using System;
using System.Reflection;
using Mono.Cecil;
using System.IO;
using Mono.Cecil.Cil;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay
{
    public class RPCILPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        private const string attributeSearchAssemblyName = "ILPostprocessorAttributes";

        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }
        private static string GetAssemblyLocation (AssemblyNameReference name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name).Location;
        private static string GetAssemblyLocation (string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name).Location;

        private class AssemblyResolver : BaseAssemblyResolver
        {
            private DefaultAssemblyResolver _defaultResolver;
            public AssemblyResolver()
            {
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                string assemblyLocation = GetAssemblyLocation(name);
                try
                {

                    parameters.AssemblyResolver = this;
                    parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                    return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assemblyLocation)), parameters);

                } catch (AssemblyResolutionException ex)
                {
                    Debug.LogException(ex);
                    return null;
                }
            }
        }

        private bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
        {
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(),
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                ReadingMode = ReadingMode.Immediate,
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            try
            {
                assemblyDef = AssemblyDefinition.ReadAssembly(peStream, readerParameters);
                return true;
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                assemblyDef = null;
                return false;
            }
        }

        private bool TryGetMethodDefinition (TypeDefinition typeDefinition, ref SerializedRPC inRPCTokenizer, out MethodDefinition methodDefinition)
        {
            var rpcTokenizer = inRPCTokenizer;
            return (methodDefinition = typeDefinition.Methods.Where(methodDef =>
            {
                // Debug.Log($"Method Signature: {methodDef.Name} == {rpcTokenizer.MethodName} &&\n{methodDef.ReturnType.Resolve().Module.Assembly.Name.Name} == {rpcTokenizer.DeclaringReturnTypeAssemblyName} &&\n{methodDef.HasParameters} == {rpcTokenizer.ParameterCount > 0} &&\n{methodDef.Parameters.Count} == {rpcTokenizer.ParameterCount}");
                bool allMatch = 
                    methodDef.HasParameters == rpcTokenizer.ParameterCount > 0 &&
                    methodDef.Parameters.Count == rpcTokenizer.ParameterCount &&
                    methodDef.Name == rpcTokenizer.methodName &&
                    methodDef.ReturnType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.declaringReturnTypeAssemblyName &&
                    methodDef.Parameters.All(parameterDefinition =>
                    {
                        if (rpcTokenizer.ParameterCount == 0)
                            return true;

                        bool any = false;
                        for (int i = 0; i < rpcTokenizer.ParameterCount; i++)
                        {
                            // Debug.Log($"Method Parameters: {parameterDefinition.Name} == {rpcTokenizer[i].parameterName} &&\n{parameterDefinition.ParameterType.FullName} == {rpcTokenizer[i].parameterTypeFullName} &&\n{parameterDefinition.ParameterType.Module.Assembly.Name.Name} == {rpcTokenizer[i].declaringParameterTypeAssemblyName}");
                            any |=
                                parameterDefinition.Name == rpcTokenizer[i].parameterName &&
                                parameterDefinition.ParameterType.FullName == rpcTokenizer[i].parameterTypeFullName &&
                                parameterDefinition.ParameterType.Resolve().Module.Assembly.Name.Name == rpcTokenizer[i].declaringParameterTypeAssemblyName;
                        }
                        return any;
                    });

                return allMatch;

            }).FirstOrDefault()) != null;
        }

        private bool TryGetOpCodeForParameter (int parameterIndex, out OpCode opCode)
        {
            opCode = OpCodes.Nop;
            switch (parameterIndex)
            {
                case 0:
                    opCode = OpCodes.Ldarg_1;
                    return true;
                case 1:
                    opCode = OpCodes.Ldarg_2;
                    return true;
                case 2:
                    opCode = OpCodes.Ldarg_3;
                    return true;
            }

            return false;
        }

        private void InjectOpenRPCLatchCall (
            ILProcessor il, 
            Instruction afterInstruction, 
            bool isStatic, 
            ushort rpcId, 
            MethodReference call, 
            ushort sizeOfAllParameters,
            out Instruction lastInstruction)
        {
            Instruction newInstruct = null;
            if (isStatic)
            {
                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertAfter(afterInstruction, newInstruct);
                lastInstruction = newInstruct;
            }

            else
            {
                newInstruct = Instruction.Create(OpCodes.Ldarg_0);
                il.InsertAfter(afterInstruction, newInstruct);
                lastInstruction = newInstruct;

                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertAfter(lastInstruction, newInstruct);
                lastInstruction = newInstruct;
            }

            newInstruct = Instruction.Create(OpCodes.Ldc_I4, sizeOfAllParameters);
            il.InsertAfter(lastInstruction, newInstruct);
            lastInstruction = newInstruct;

            newInstruct = Instruction.Create(OpCodes.Call, call);
            il.InsertAfter(lastInstruction, newInstruct);
            lastInstruction = newInstruct;
        }

        private bool TryDetermineSizeOfPrimitive (string typeName, ref int size)
        {
            switch (typeName)
            {
                case "Byte":
                case "SByte":
                case "Boolean":
                    size += 1;
                    return true;
                case "Int16":
                case "UInt16":
                case "Char":
                    size += 2;
                    return true;
                case "Int32":
                case "UInt32":
                case "Single":
                    size += 4;
                    return true;
                case "Int64":
                case "UInt64":
                case "Double":
                    size += 8;
                    return true;
                default:
                    Debug.LogError($"Unable to determine size of assumed primitive type: \"{typeName}\".");
                    return false;
            }
        }

        private bool TryDetermineSizeOfStruct (TypeDefinition typeDefinition, ref int size)
        {
            bool allValid = true;

            foreach (var field in typeDefinition.Fields)
            {
                if (field.IsStatic)
                    continue;
                allValid &= TryDetermineSizeOfType(field.FieldType.Resolve(), ref size);
            }

            return allValid;
        }

        private bool TryDetermineSizeOfType (TypeDefinition typeDefinition, ref int size)
        {
            if (typeDefinition.IsPrimitive || typeDefinition.IsEnum)
                return TryDetermineSizeOfPrimitive(typeDefinition.Name, ref size);
            else if (typeDefinition.IsValueType)
                return TryDetermineSizeOfStruct(typeDefinition, ref size);
            return false;
        }

        private bool TryFindMethodDefinitionWithAttribute (TypeDefinition typeDef, TypeReference attributeTypeRef, out MethodDefinition methodDef)
        {
            var found = (methodDef = typeDef.Methods
                .Where(method => method.CustomAttributes
                    .Any(customAttribute =>
                    {
                        return
                            customAttribute.AttributeType.FullName == attributeTypeRef.FullName;
                    })).FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find method definition with attribute: \"{attributeTypeRef.FullName}\" in type: \"{typeDef.FullName}");

            return found;
        }

        private bool TryFindMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
        {
            var attributeType = typeof(T);
            var found = (methodInfo = type.GetMethods()
                .Where(method => method.CustomAttributes
                    .Any(customAttribute =>
                    {
                        return
                            customAttribute.AttributeType.FullName == attributeType.FullName;
                    })).FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find method info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}");

            return found;
        }

        private bool TryFindPropertyGetMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
        {
            methodInfo = null;
            var attributeType = typeof(T);
            var propertyInfo = type.GetProperties()
                .Where(pi => pi.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault();

            if (propertyInfo == null || (methodInfo = propertyInfo.GetGetMethod()) == null)
                Debug.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}");

            return methodInfo != null;
        }

        private bool TryBridge (
            AssemblyDefinition assemblyDef, 
            ushort rpcId, 
            MethodDefinition targetMethod)
        {
            var beforeInstruction = targetMethod.Body.Instructions.First();
            var il = targetMethod.Body.GetILProcessor();
            var rpcEmitterType = typeof(RPCEmitter);

            var rpcEmitterTypeReference = assemblyDef.MainModule.ImportReference(rpcEmitterType);

            MethodInfo appendRPCMethodInfo = null;
            if (!targetMethod.IsStatic)
            {
                if (!TryFindMethodWithAttribute<RPCEmitter.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;
            }

            else if (!TryFindMethodWithAttribute<RPCEmitter.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                return false;

            if (!TryFindMethodWithAttribute<RPCEmitter.CopyValueToBufferMarker>(rpcEmitterType, out var copyValueToBufferMethodInfo))
                return false;

            var openRPCLatchMethodRef = assemblyDef.MainModule.ImportReference(appendRPCMethodInfo);
            var copyValueToBufferMethodRef = assemblyDef.MainModule.ImportReference(copyValueToBufferMethodInfo);

            var parameters = targetMethod.Parameters;
            ushort sizeOfAllParameters = 0;
            foreach (var param in parameters)
            {
                var typeReference = assemblyDef.MainModule.ImportReference(param.ParameterType);

                int sizeOfType = 0;
                if (!TryDetermineSizeOfType(typeReference.Resolve(), ref sizeOfType))
                    return false;

                if (sizeOfType > ushort.MaxValue || ((int)sizeOfAllParameters) + sizeOfType > ushort.MaxValue)
                    return false;

                sizeOfAllParameters += (ushort)sizeOfType;
            }

            Instruction newInstruction = null;
            Instruction previousInstruction = null;

            if (!TryGetCachedGetIsMasterMarkerMethod(out var getIsMasterMethod))
                return false;

            newInstruction = Instruction.Create(OpCodes.Call, assemblyDef.MainModule.ImportReference(getIsMasterMethod));
            il.InsertBefore(beforeInstruction, newInstruction);
            previousInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Brfalse_S, beforeInstruction);
            il.InsertAfter(previousInstruction, newInstruction);
            previousInstruction = newInstruction;

            InjectOpenRPCLatchCall(
                il, 
                previousInstruction, 
                targetMethod.IsStatic, 
                rpcId, 
                openRPCLatchMethodRef, 
                sizeOfAllParameters,
                out previousInstruction);

            int parameterIndex = 0;
            foreach (var param in parameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(copyValueToBufferMethodRef);
                genericInstanceMethod.GenericArguments.Add(param.ParameterType);

                if (!TryGetOpCodeForParameter(parameterIndex++, out var opCode))
                    return false;

                newInstruction = Instruction.Create(opCode);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethod);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));

            return true;
        }

        private void InjectSwitchCase(
            ModuleDefinition moduleDef,
            ILProcessor il,
            Instruction beforeInstruction,
            MethodReference objectRegistryGetItemMethodRef,
            MethodReference targetMethod,
            out Instruction firstInstruction)
        {
             // Load "objectRegistry" local variable.
            firstInstruction = Instruction.Create(OpCodes.Ldloc_0);
            il.InsertBefore(beforeInstruction, firstInstruction);
            var afterInstruction = firstInstruction;

             // Load pipeId parameter onto stack.
            var newInstruction = Instruction.Create(OpCodes.Ldarg_1);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            // Call objectRegistry[pipeId].
            newInstruction = Instruction.Create(OpCodes.Callvirt, objectRegistryGetItemMethodRef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            if (!targetMethod.HasParameters)
            {
                // Call method on target object without any parameters.
                newInstruction = Instruction.Create(OpCodes.Callvirt, targetMethod);
                il.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
                il.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
                il.InsertAfter(afterInstruction, newInstruction);
                return;
            }

            // Get static ParseStructure method in RPCEmitter type.
            if (!TryFindMethodWithAttribute<RPCEmitter.ParseStructureMarker>(typeof(RPCEmitter), out var parseStructureMethod))
                return;

             // Import the ParseStructureMethod method.
            var parseStructureMethodRef = moduleDef.ImportReference(parseStructureMethod);

             // Get the first parameter of the method we are editing.
            var startPosParamDef = il.Body.Method.Parameters.Where(paramDef => paramDef.Name == "startPos").FirstOrDefault();

            // Loop through all parameters of the method we want to call on our object.
            foreach (var parameterReference in targetMethod.Parameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(parseStructureMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                var paramRef = moduleDef.ImportReference(parameterReference.ParameterType);
                paramRef.IsValueType = true;
                genericInstanceMethod.GenericArguments.Add(paramRef);
                var genericInstanceMethodRef = moduleDef.ImportReference(genericInstanceMethod);

                newInstruction = Instruction.Create(OpCodes.Ldarg_3); // Load startPos onto stack as an argument.
                il.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethodRef); // Call generic method to convert bytes into our struct.
                il.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;
            }

            newInstruction = Instruction.Create(OpCodes.Callvirt, targetMethod); // Call our method on the target object with all the parameters.
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
            il.InsertAfter(afterInstruction, newInstruction);
        }

        private bool GetOnTryCallILProcessor (AssemblyDefinition assemblyDef, out TypeReference derrivedTypeRef, out ILProcessor il)
        {
            var rpcInterfaceRegistryType = typeof(RPCInterfaceRegistry);
            var onTryCallMarkerAttributeType = typeof(RPCInterfaceRegistry.OnTryCallMarker);

            var rpcInstanceRegistryTypeDef = assemblyDef.MainModule.ImportReference(rpcInterfaceRegistryType).Resolve();
            var onTryCallMarkerAttributeTypeRef = assemblyDef.MainModule.ImportReference(onTryCallMarkerAttributeType);

            var derrivedTypeDef = assemblyDef.MainModule.GetTypes()
                .Where(typeDef => 
                    typeDef != null && 
                    typeDef.BaseType != null && 
                    typeDef.BaseType.FullName == rpcInstanceRegistryTypeDef.FullName).FirstOrDefault();

            if (derrivedTypeDef == null)
            {
                derrivedTypeRef = null;
                il = null;
                return false;
            }

            if (!TryFindMethodDefinitionWithAttribute(derrivedTypeDef, onTryCallMarkerAttributeTypeRef, out var onTryCallMethod))
            {
                derrivedTypeRef = null;
                il = null;
                return false;
            }

            var onTryCallMethodDef = assemblyDef.MainModule.ImportReference(onTryCallMethod);
            derrivedTypeRef = onTryCallMethodDef.DeclaringType;
            il = onTryCallMethodDef.Resolve().Body.GetILProcessor();

            return true;
        }

        private bool TryImportTryGetSingletonObject<T> (
            ModuleDefinition moduleDefinition,
            out TypeReference typeRef,
            out MethodReference tryGetInstanceMethodRef)
        {
            var type = typeof(T);
            typeRef = moduleDefinition.ImportReference(type);

            if (typeRef == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var typeDef = typeRef.Resolve();
            if (type.BaseType == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseTypeRef = moduleDefinition.ImportReference(type.BaseType);
            if (baseTypeRef == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericType = new GenericInstanceType(baseTypeRef);
            baseGenericType.GenericArguments.Add(typeRef);

            if (!TryFindMethodWithAttribute<SingletonScriptableObjectTryGetInstanceMarker>(type.BaseType, out var tryGetInstanceMethod))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericMethodRef = moduleDefinition.ImportReference(tryGetInstanceMethod);

            return (tryGetInstanceMethodRef = baseGenericMethodRef) != null;
        }

        private bool TryInjectTryGetObjectRegistryInstance (
            ModuleDefinition moduleDefinition,
            out TypeReference objectRegistryTypeRef,
            out MethodReference objectRegistryTryGetInstanceMethodRef,
            out MethodReference objectRegistryGetItemMethodRef)
        {
            if (!TryImportTryGetSingletonObject<ObjectRegistry>(moduleDefinition, out objectRegistryTypeRef, out objectRegistryTryGetInstanceMethodRef))
            {
                objectRegistryTypeRef = null;
                objectRegistryTryGetInstanceMethodRef = null;
                objectRegistryGetItemMethodRef = null;
                return false;
            }

            var objectRegistryTypeDef = objectRegistryTypeRef.Resolve();
            objectRegistryGetItemMethodRef = moduleDefinition.ImportReference(objectRegistryTypeDef.Methods.Where(method => method.Name == "get_Item").FirstOrDefault());

            return objectRegistryTypeDef != null && objectRegistryTryGetInstanceMethodRef != null && objectRegistryGetItemMethodRef != null;
        }

        private void InjectSwitchJmp (
            ILProcessor il,
            Instruction afterInstruction,
            ushort rpcId,
            Instruction targetInstruction,
            out Instruction lastInstruction)
        {
            var newInstruction = Instruction.Create(OpCodes.Ldarg_2);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4, rpcId);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Beq, targetInstruction);
            il.InsertAfter(afterInstruction, newInstruction);
            lastInstruction = newInstruction;
        }

        private void InsertDebugMessage (
            ModuleDefinition moduleDef,
            ILProcessor il,
            string message,
            Instruction afterInstruction,
            out Instruction lastInstruction)
        {
            var debugType = typeof(Debug);
            var debugTypeRef = moduleDef.ImportReference(debugType);
            var debugTypeDef = debugTypeRef.Resolve();
            var logMethodRef = moduleDef.ImportReference(debugTypeDef.Methods.Where(method => {
                return
                    method.Name == "Log" &&
                    method.Parameters.Count == 1;
            }).FirstOrDefault());

            lastInstruction = null;

            var newInstruction = Instruction.Create(OpCodes.Ldstr, message);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Call, logMethodRef);
            il.InsertAfter(afterInstruction, newInstruction);
            lastInstruction = newInstruction;
        }

        private void InjectObjectRegistryTryGet(
            ModuleDefinition moduleDef,
            ILProcessor onTryCallILProcessor,
            TypeDefinition objectRegistryTypeDef,
            MethodReference objectRegistryTryGetInstance,
            Instruction afterInstruction,
            out Instruction tryGetInstanceFailureInstruction,
            out Instruction lastInstruction)
        {
            var objectRegistryLocalVariable = new VariableDefinition(moduleDef.ImportReference(objectRegistryTypeDef));
            onTryCallILProcessor.Body.Variables.Add(objectRegistryLocalVariable);

            var newInstruction = Instruction.Create(OpCodes.Ldloca_S,  onTryCallILProcessor.Body.Variables[0]);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4_0);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Call, objectRegistryTryGetInstance);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            tryGetInstanceFailureInstruction = Instruction.Create(OpCodes.Brfalse, onTryCallILProcessor.Body.Instructions[0]);
            onTryCallILProcessor.InsertAfter(afterInstruction, tryGetInstanceFailureInstruction);
            lastInstruction = tryGetInstanceFailureInstruction;
        }

        private MethodInfo cachedGetIsMasterMethod;
        private bool TryGetCachedGetIsMasterMarkerMethod (out MethodInfo getIsMasterMethod)
        {
            if (!TryFindPropertyGetMethodWithAttribute<ClusterDisplayState.IsMasterMarker>(typeof(ClusterDisplayState), out cachedGetIsMasterMethod))
            {
                getIsMasterMethod = null;
                return false;
            }

            getIsMasterMethod = cachedGetIsMasterMethod;
            return true;
        }

        private bool TrySetup (
            ICompiledAssembly compiledAssembly, 
            out AssemblyDefinition assemblyDef,
            out SerializedRPC[] serializedRPCs)
        {
            assemblyDef = null;
            serializedRPCs = null;

            if (compiledAssembly.Name != ReflectionUtils.DefaultUserAssemblyName)
                return false;

            if (!RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out serializedRPCs))
                return false;

            if (serializedRPCs.Length == 0)
                return false;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out assemblyDef))
                return false;

            return true;
        }

        private bool TrySetupOnTryCall (
            AssemblyDefinition assemblyDef, 
            out ILProcessor onTryCallILProcessor, 
            out ModuleDefinition rpcInterfacesModule, 
            out Instruction firstInstruction,
            out Instruction beginningOfFailureInstruction)
        {
            if (!GetOnTryCallILProcessor(assemblyDef, out var rpcInterfacesTypeRef, out onTryCallILProcessor))
            {
                rpcInterfacesModule = null;
                firstInstruction = null;
                beginningOfFailureInstruction = null;
                return false;
            }

            onTryCallILProcessor.Body.Instructions.Clear();
            onTryCallILProcessor.Body.Variables.Clear();
            onTryCallILProcessor.Body.InitLocals = false;

            firstInstruction = Instruction.Create(OpCodes.Nop);
            onTryCallILProcessor.Append(firstInstruction);

            beginningOfFailureInstruction = Instruction.Create(OpCodes.Ldc_I4_0);
            onTryCallILProcessor.Append(beginningOfFailureInstruction);
            onTryCallILProcessor.Append(Instruction.Create(OpCodes.Ret));

            rpcInterfacesModule = rpcInterfacesTypeRef.Module;
            return true;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!TrySetup(
                compiledAssembly,
                out var assemblyDef,
                out var serializedRPCs))
                return null;

            if (!TrySetupOnTryCall(
                assemblyDef,
                out var onTryCallILProcessor,
                out var rpcInterfacesModule,
                out var firstInstruction,
                out var beginningOfFailureInstruction))
                return null;

            if (!TryInjectTryGetObjectRegistryInstance(
                rpcInterfacesModule,
                out var objectRegistryTypeRef,
                out var objectRegistryTryGetInstanceMethodRef,
                out var objectRegistryTryGetItemMethodRef))
                return null;

            Instruction lastSwitchJmpInstruction = null;

            /*
            InsertDebugMessage(
                rpcInterfacesModule,
                onTryCallILProcessor,
                "TEST",
                firstInstruction,
                out lastSwitchJmpInstruction);
            */

            lastSwitchJmpInstruction = firstInstruction;
            InjectObjectRegistryTryGet(
                rpcInterfacesModule,
                onTryCallILProcessor,
                objectRegistryTypeRef.Resolve(),
                objectRegistryTryGetInstanceMethodRef,
                afterInstruction: lastSwitchJmpInstruction,
                out var tryGetInstanceFailureInstruction,
                lastInstruction: out lastSwitchJmpInstruction);

            foreach (var serializedRPC in serializedRPCs)
            {
                var rpc = serializedRPC;
                if (rpc.declaringAssemblyName != compiledAssembly.Name)
                    continue;

                Debug.Log($"Post processing compiled assembly: \"{compiledAssembly.Name}\".");

                var typeDefinition = assemblyDef.MainModule.GetType(rpc.declaryingTypeFullName);
                if (!TryGetMethodDefinition(typeDefinition, ref rpc, out var methodDefinition))
                {
                    Debug.LogError($"Unable to find method signature: \"{rpc.methodName}\".");
                    continue;
                }

                if (!TryBridge(
                    assemblyDef, 
                    rpc.rpcId, 
                    methodDefinition))
                    continue;

                InjectSwitchCase(
                    rpcInterfacesModule,
                    onTryCallILProcessor,
                    beforeInstruction: beginningOfFailureInstruction,
                    objectRegistryTryGetItemMethodRef,
                    methodDefinition,
                    firstInstruction: out var startOfSwitchCaseInstruction);

                InjectSwitchJmp(
                    onTryCallILProcessor,
                    afterInstruction: lastSwitchJmpInstruction,
                    rpc.rpcId,
                    targetInstruction: startOfSwitchCaseInstruction,
                    lastInstruction: out lastSwitchJmpInstruction);

                Debug.Log($"Injected RPC intercept assembly into method: \"{methodDefinition.Name}\" in class: \"{methodDefinition.DeclaringType.FullName}\".");
            }

            tryGetInstanceFailureInstruction.Operand = beginningOfFailureInstruction;

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            try
            {
                assemblyDef.Write(pe, writerParameters);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return true;
        }
    }
}
