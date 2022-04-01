using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.RPC
{
    internal static class RPCSerializer
    {
        // Nested address are formatted in the following way: {root type namespace}.{root type}/{nested type}/{nested nested type}/{nested nested nested type}
        public static bool TryParseNestedAddressIfAvailable (string addressStr, out string rootTypeNamespace, out string[] nestedTypeNames)
        {
            if (string.IsNullOrEmpty(addressStr))
            {
                rootTypeNamespace = null;
                nestedTypeNames = null;
                return false;
            }
            
            var nestedSplit = addressStr.Split('/'); // Nested types are embedded in the namespace and separated by the "/" character.
            if (nestedSplit.Length == 1) // Type is nested, so we need to walk up the nesting hierarchy finding types.
            {
                rootTypeNamespace = null;
                nestedTypeNames = null;
                return false;
            }

            rootTypeNamespace = nestedSplit[0]; // The first element should always be the {namespace}.{root type}
            nestedTypeNames = new string[nestedSplit.Length];

            int lastIndexOfDot = rootTypeNamespace.LastIndexOf('.');
            if (lastIndexOfDot == -1)
            {
                rootTypeNamespace = null;
                System.Array.Copy(nestedSplit, 0, nestedTypeNames, 0, nestedTypeNames.Length);
                return true;
            }
            
            nestedTypeNames[0] = rootTypeNamespace.Substring(lastIndexOfDot + 1, rootTypeNamespace.Length - (lastIndexOfDot + 1));
            rootTypeNamespace = rootTypeNamespace.Substring(0, lastIndexOfDot);

            System.Array.Copy(nestedSplit, 1, nestedTypeNames, 1, nestedTypeNames.Length - 1);
            return true;
        }

        public static bool TryDeserializeType (string assemblyStr, string namespaceStr, string typeStr, out System.Type type)
        {
            type = null;
            if (string.IsNullOrEmpty(assemblyStr) || string.IsNullOrEmpty(typeStr))
            {
                CodeGenDebug.LogError("Unable to deserialize type, either the assembly or type string is null or invalid!");
                return false;
            }

            if (!ReflectionUtils.TryGetAssemblyByName(assemblyStr, out var assembly))
                return false;

            if (!string.IsNullOrEmpty(namespaceStr))
            {
                if (TryParseNestedAddressIfAvailable(namespaceStr, out var rootNamespace, out var nestedTypeNames))
                {
                    System.Type[] containerTypes = new Type[nestedTypeNames.Length];
                    if (string.IsNullOrEmpty(rootNamespace))
                        ReflectionUtils.TryFindTypeByName(nestedTypeNames[0], out containerTypes[0]);
                    else ReflectionUtils.TryFindTypeByNamespaceAndName(rootNamespace, nestedTypeNames[0], out containerTypes[0]);

                    if (containerTypes[0] == null)
                    {
                        CodeGenDebug.LogError($"Unable to find nested type: \"{typeStr}\", cannot find the root type: \"{nestedTypeNames[0]}\" in our nested type hierarchy: \"{namespaceStr}\".");
                        return false;
                    }

                    for (int i = 1; i < containerTypes.Length; i++)
                    {
                        ReflectionUtils.ForeachNestedType(containerTypes[i - 1], (nestedType) =>
                        {
                            if (nestedType.Name != nestedTypeNames[i])
                                return true;

                            containerTypes[i] = nestedType;
                            return false;
                        });

                        if (containerTypes[i] == null)
                        {
                            CodeGenDebug.LogError($"Unable to find nested type: \"{typeStr}\", cannot find container type: \"{nestedTypeNames[i]} from nested type hierarchy: \"{namespaceStr}\".");
                            return false;
                        }
                    }

                    type = containerTypes[containerTypes.Length - 1];
                    return true;
                }

                return ReflectionUtils.TryFindTypeByNamespaceAndName(namespaceStr, typeStr, out type);
            }

            return ReflectionUtils.TryFindTypeByName(typeStr, out type);
        }

        private static Dictionary<System.Type, MethodInfo[]> cachedTypeMethodInfos = new Dictionary<System.Type, MethodInfo[]>();
        public static bool TryDeserializeMethodInfo (RPMethodStub methodStub, out MethodInfo outMethodInfo)
        {
            outMethodInfo = null;

            if (!TryDeserializeType(methodStub.declaringAssemblyName, methodStub.declaringTypeNamespace, methodStub.declaringTypeName, out var declaringType))
            {
                CodeGenDebug.LogError($"Unable to find serialized method's declaring type: \"{methodStub.declaringTypeName}\" in assembly: \"{methodStub.declaringAssemblyName}\".");
                return false;
            }

            if (!TryDeserializeType(methodStub.declaringReturnTypeAssemblyName, methodStub.returnTypeNamespace, methodStub.returnTypeName, out var returnType))
            {
                CodeGenDebug.LogError($"Unable to find serialized method's return type: \"{methodStub.returnTypeName}\" in assembly: \"{methodStub.declaringReturnTypeAssemblyName}\".");
                return false;
            }

            var parameterList = new List<(System.Type parameterType, string parameterName)>();
            if (methodStub.ParameterCount > 0)
            {
                for (int i = 0; i < methodStub.ParameterCount; i++)
                {
                    var parameterString = methodStub[i];
                    if (!TryDeserializeType(parameterString.declaringParameterTypeAssemblyName, parameterString.parameterTypeNamespace, parameterString.parameterTypeName, out var parameterType))
                    {
                        CodeGenDebug.LogError($"Unable to find serialize method's parameter type: \"{parameterString.parameterTypeName}\" in assembly: \"{parameterString.declaringParameterTypeAssemblyName}\".");
                        return false;
                    }

                    parameterList.Add((parameterType, parameterString.parameterName));
                }
            }

            MethodInfo matchingMethod = null;
            ReflectionUtils.ParallelForeachMethod(declaringType, (methodInfo) =>
            {
                if (methodInfo.Name != methodStub.methodName)
                    return true;

                var parameters = methodInfo.GetParameters();
                if (parameterList.Count > 0)
                {
                    if (parameters.Length != parameterList.Count)
                        return true;

                    bool allMatching = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType.IsGenericType)
                            allMatching &=
                                parameters[i].ParameterType.GetGenericTypeDefinition() == parameterList[i].parameterType &&
                                parameters[i].Name == parameterList[i].parameterName;

                        else
                            allMatching &=
                           parameters[i].ParameterType == parameterList[i].parameterType &&
                           parameters[i].Name == parameterList[i].parameterName;
                    }

                    if (allMatching)
                        goto found;
                }

                else if (parameters.Length == 0)
                    goto found;

                return true;

                found:
                matchingMethod = methodInfo;
                return false;

            });

            if (matchingMethod == null)
            {
                CodeGenDebug.LogError($"Unable to deserialize method: \"{methodStub.methodName}\", declared in type: \"{methodStub.declaringTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                return false;
            }

            if (!matchingMethod.IsPublic)
            {
                CodeGenDebug.LogError($"Unable to use method deserialized method: \"{matchingMethod.Name}\" declared in type: \"{(string.IsNullOrEmpty(matchingMethod.DeclaringType.Namespace) ? matchingMethod.DeclaringType.Name  : $"{matchingMethod.DeclaringType.Namespace}.{matchingMethod.DeclaringType.Name}")}\", the method must be public.");
                return false;
            }

            outMethodInfo = matchingMethod;
            return true;
        }

        public static bool TryDeserializeMethodInfo (RPCStub rpcStub, out RPCExecutionStage rpcExecutionStage, out MethodInfo outMethodInfo)
        {
            rpcExecutionStage = (RPCExecutionStage)rpcStub.rpcExecutionStage;
            return TryDeserializeMethodInfo(rpcStub.methodStub, out outMethodInfo);
        }

        private static void MakeFileWriteable(string path)
        {
            if (!File.Exists(path))
                return;
            
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                CodeGenDebug.Log($"Making file: \"{path}\" writeable");
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private static void Serialize<T> (T[] rpcs, out byte[] bytes) where T : struct
        {
            if (rpcs == null || rpcs.Length == 0)
            {
                bytes = null;
                return;
            }
            
            var count = rpcs.Length;
            var size = Marshal.SizeOf<T>();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                memoryStream.Write(BitConverter.GetBytes(count));

                for (int i = 0; i < rpcs.Length; i++)
                {
                    byte[] buffer = new byte[size];

                    var ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(rpcs[i], ptr, true);
                    Marshal.Copy(ptr, buffer, 0, size);

                    memoryStream.Write(buffer);
                }

                bytes = memoryStream.GetBuffer();
            }
        }

        public static bool SerializeRPCs (
            RPCStub[] rpcs, 
            RPMethodStub[] stagedRPCs, 
            out byte[] rpcBytes,
            out byte[] stagedRPCBytes)
        {
            rpcBytes = null;
            stagedRPCBytes = null;
            
            try
            {
                Serialize(rpcs, out rpcBytes);
                Serialize(stagedRPCs, out stagedRPCBytes);
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogError($"Unable to serialize RPC stubs, the following exception occurred.");
                CodeGenDebug.LogException(exception);
                return false;
            }

            return true;
        }

        private static void Read<T> (string path, out T[] data, bool logMissingFile) where T : struct
        {
            data = new T[0];
            if (string.IsNullOrEmpty(path))
            {
                CodeGenDebug.LogError("Unable to read RPC stubs, the path is invalid.");
                return;
            }

            if (!File.Exists(path))
            {
                if (logMissingFile)
                    CodeGenDebug.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    return;
                
                BytesToRPCs<T>(bytes, out data);
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                CodeGenDebug.LogException(exception);
            }
        }

        public static void ReadAllRPCs (
            string rpcsPath,
            string stagedRPCsPath, 
            out RPCStub[] rpcs, 
            out RPMethodStub[] stagedRPCs, 
            bool logMissingFile = true)
        {
            Read(rpcsPath, out rpcs, logMissingFile);
            Read(stagedRPCsPath, out stagedRPCs, logMissingFile);
        }

        public unsafe static void BytesToRPCs<T> (
            byte[] bytes, 
            out T[] rpcs) where T : struct
        {
            if (bytes == null || bytes.Length == 0)
            {
                CodeGenDebug.LogError("Unable to parse RPCs from bytes, the byte array is NULL!");
                rpcs = new T[0];
                return;
            }
            
            try
            {
                int count = BitConverter.ToInt32(bytes, 0);
                var size = Marshal.SizeOf<T>();
                
                rpcs = new T[count];

                fixed (byte * ptr = bytes)
                {
                    int byteOffset = 4;
                    for (int i = 0; i < count; i++)
                    {
                        var intPtr = new IntPtr(ptr + byteOffset);
                        rpcs[i] = Marshal.PtrToStructure<T>(intPtr);
                        byteOffset += size;
                    }
                }
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogError($"Unable to parse RPC stubs from bytes, the following exception occurred.");
                CodeGenDebug.LogException(exception);
                rpcs = new T[0];
            }
        }

        private static bool Write(string path, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return true;
            
            if (string.IsNullOrEmpty(path))
            {
                CodeGenDebug.LogError("Unable to write serialized RPCs, the path is invalid.");
                return false;
            }
            
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                
                MakeFileWriteable(path);
                File.WriteAllBytes(path, bytes);
                
            } catch (Exception exception)
            {
                CodeGenDebug.LogError($"Unable to write serialized RPCs to path: \"{path}\", the following exception occurred.");
                CodeGenDebug.LogException(exception);
                return false;
            }

            return true;
        }

        public static bool WriteSerializedRPCs (
            string rpcsPath,
            string stagedRPCsPath, 
            byte[] rpcBytes,
            byte[] stagedRPCBytes) =>
                Write(rpcsPath, rpcBytes) &&
                Write(stagedRPCsPath, stagedRPCBytes);
    }
}
