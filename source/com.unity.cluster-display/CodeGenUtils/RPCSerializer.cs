using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public static class RPCSerializer
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
                nestedTypeNames = null;
                return false;
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
                LogWriter.LogError("Unable to deserialize type, either the assembly or type string is null or invalid!");
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
                        LogWriter.LogError($"Unable to find nested type: \"{typeStr}\", cannot find the root type: \"{nestedTypeNames[0]}\" in our nested type hierarchy: \"{namespaceStr}\".");
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
                            LogWriter.LogError($"Unable to find nested type: \"{typeStr}\", cannot find container type: \"{nestedTypeNames[i]} from nested type hierarchy: \"{namespaceStr}\".");
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
        public static bool TryDeserializeMethodInfo (SerializedMethod method, out MethodInfo outMethodInfo)
        {
            outMethodInfo = null;

            if (!TryDeserializeType(method.declaringAssemblyName, method.declaringTypeNamespace, method.declaryingTypeName, out var declaringType))
            {
                LogWriter.LogError($"Unable to find serialized method's declaring type: \"{method.declaryingTypeName}\" in assembly: \"{method.declaringAssemblyName}\".");
                return false;
            }

            if (!TryDeserializeType(method.declaringReturnTypeAssemblyName, method.returnTypeNamespace, method.returnTypeName, out var returnType))
            {
                LogWriter.LogError($"Unable to find serialized method's return type: \"{method.returnTypeName}\" in assembly: \"{method.declaringReturnTypeAssemblyName}\".");
                return false;
            }

            var parameterList = new List<(System.Type parameterType, string parameterName)>();
            if (method.ParameterCount > 0)
            {
                for (int i = 0; i < method.ParameterCount; i++)
                {
                    var parameterString = method[i];
                    if (!TryDeserializeType(parameterString.declaringParameterTypeAssemblyName, parameterString.parameterTypeNamespace, parameterString.parameterTypeName, out var parameterType))
                    {
                        LogWriter.LogError($"Unable to find serialize method's parameter type: \"{parameterString.parameterTypeName}\" in assembly: \"{parameterString.declaringParameterTypeAssemblyName}\".");
                        return false;
                    }

                    parameterList.Add((parameterType, parameterString.parameterName));
                }
            }

            MethodInfo matchingMethod = null;
            ReflectionUtils.ParallelForeachMethod(declaringType, (methodInfo) =>
            {
                if (methodInfo.Name != method.methodName)
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
                LogWriter.LogError($"Unable to deserialize method: \"{method.methodName}\", declared in type: \"{method.declaryingTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                return false;
            }

            if (!matchingMethod.IsPublic)
            {
                LogWriter.LogError($"Unable to use method deserialized method: \"{matchingMethod.Name}\" declared in type: \"{(string.IsNullOrEmpty(matchingMethod.DeclaringType.Namespace) ? matchingMethod.DeclaringType.Name  : $"{matchingMethod.DeclaringType.Namespace}.{matchingMethod.DeclaringType.Name}")}\", the method must be public.");
                return false;
            }

            outMethodInfo = matchingMethod;
            return true;
        }

        public static bool TryDeserializeMethodInfo (SerializedRPC serializedRPC, out RPCExecutionStage rpcExecutionStage, out MethodInfo outMethodInfo)
        {
            rpcExecutionStage = (RPCExecutionStage)serializedRPC.rpcExecutionStage;
            return TryDeserializeMethodInfo(serializedRPC.method, out outMethodInfo);
        }

        public static void PrepareRegisteredAssembliesForSerialization (List<Assembly> assemblies, out string serializedAssemblies)
        {
            serializedAssemblies = "";
            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;
                serializedAssemblies = $"{serializedAssemblies}{Environment.NewLine}{assembly.GetName().FullName}";
            }
        }

        private static void MakeFileWriteable(string path)
        {
            if (!File.Exists(path))
                return;
            
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                Debug.Log($"Making file: \"{path}\" writeable");
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        public static bool TryWriteRegisteredAssemblies (string path, string serializedAssemblies)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogWriter.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                MakeFileWriteable(path);
                System.IO.File.WriteAllText(path, serializedAssemblies);

            } catch (System.Exception exception)
            {
                LogWriter.LogError($"Unable to write registered assemblies to path: \"{path}\", the following exception occurred.");
                LogWriter.LogException(exception);
                return false;
            }

            // LogWriter.Log($"RPC Stubs was written to path: \"{path}\".");
            return true;
        }

        public static void ReadRegisteredAssemblies (string path, out string[] registeredAssemblyFullNames)
        {
            registeredAssemblyFullNames = new string[0];
            if (string.IsNullOrEmpty(path))
            {
                LogWriter.LogWarning("Unable to read registered assemblies, the path is invalid.");
                return;
            }

            if (!System.IO.File.Exists(path))
            {
                LogWriter.LogWarning($"Unable to read registered assemblies from path: \"{path}\", the file does not exist.");
                return;
            }

            try
            {
                var assemblyFullNames = System.IO.File.ReadAllLines(path);
                registeredAssemblyFullNames = assemblyFullNames.Where(assemblyFullName => !string.IsNullOrEmpty(assemblyFullName)).ToArray();

            } catch (System.Exception exception)
            {
                LogWriter.LogError($"Unable to read registered assemblies from path: \"{path}\", the following exception occurred.");
                LogWriter.LogException(exception);
            }

            // LogWriter.Log($"Registered assemblies was read from path: \"{path}\".");
            return;
        }

        public static bool TryDeserializeRegisteredAssemblies (string serializedRegisteredAssemblies, out List<Assembly> registeredAssemblies)
        {
            if (string.IsNullOrEmpty(serializedRegisteredAssemblies))
            {
                LogWriter.LogError("String containing serialized registered assemblies is null or empty.");
                registeredAssemblies = null;
                return false;
            }

            var registeredAssemblyFullNames = serializedRegisteredAssemblies.Split(new string[] {Environment.NewLine }, StringSplitOptions.None);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            registeredAssemblies = new List<Assembly>();

            for (int rai = 0; rai < registeredAssemblyFullNames.Length; rai++)
            {
                for (int ai = 0; ai < assemblies.Length; ai++)
                {
                    if (assemblies[ai].FullName != registeredAssemblyFullNames[rai])
                        continue;

                    registeredAssemblies.Add(assemblies[ai]);
                    break;
                }
            }

            // LogWriter.Log($"Deserialized: {registeredAssemblies.Count} registered assemblies.");
            return registeredAssemblies.Count > 0;
        }

        public static bool TryReadRegisteredAssemblies (string path, out List<Assembly> registeredAssemblies)
        {
            ReadRegisteredAssemblies(path, out var registeredAssemblyFullNames);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            registeredAssemblies = new List<Assembly>();

            for (int rai = 0; rai < registeredAssemblyFullNames.Length; rai++)
            {
                for (int ai = 0; ai < assemblies.Length; ai++)
                {
                    if (assemblies[ai].FullName != registeredAssemblyFullNames[rai])
                        continue;

                    registeredAssemblies.Add(assemblies[ai]);
                    break;
                }
            }

            return registeredAssemblies.Count > 0;
        }

        public static bool TryPrepareRPCStubsForSerialization (SerializedRPC[] rpcsToSerialize, SerializedMethod[] stagedMethods, out string jsonString)
        {
            try
            {
                var serializedInstanceRPCs = new RPCStubs
                {
                    rpcs = rpcsToSerialize,
                    stagedMethods = stagedMethods
                };

                jsonString = JsonConvert.SerializeObject(serializedInstanceRPCs, Formatting.Indented);
            }

            catch (System.Exception exception)
            {
                LogWriter.LogError($"Unable to serialize RPC stubs, the following exception occurred.");
                LogWriter.LogException(exception);
                jsonString = null;
                return false;
            }

            return true;
        }

        public static void ReadRPCStubs (string path, out SerializedRPC[] serializedInstanceRPCs, out SerializedMethod[] stagedMethods)
        {
            serializedInstanceRPCs = new SerializedRPC[0];
            stagedMethods = new SerializedMethod[0];
            if (string.IsNullOrEmpty(path))
            {
                LogWriter.LogError("Unable to read RPC stubs, the path is invalid.");
                return;
            }

            if (!System.IO.File.Exists(path))
            {
                LogWriter.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
                return;
            }

            try
            {
                var text = System.IO.File.ReadAllText(path);
                if (!TryDeserializeRPCStubsJson(text, out serializedInstanceRPCs, out stagedMethods))
                    return;

            }

            catch (System.Exception exception)
            {
                LogWriter.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                LogWriter.LogException(exception);
                return;
            }
        }

        public static bool TryDeserializeRPCStubsJson (string jsonStr, out SerializedRPC[] rpcs, out SerializedMethod[] stagedMethods)
        {
            try
            {
                ;
                var stubs = JsonConvert.DeserializeObject<RPCStubs>(jsonStr);
                rpcs = stubs.rpcs;
                stagedMethods = stubs.stagedMethods;

            }

            catch (System.Exception exception)
            {
                LogWriter.LogError($"Unable to parse RPC stubs JSON, the following exception occurred.");
                LogWriter.LogException(exception);
                rpcs = null;
                stagedMethods = null;
                return false;
            }

            // LogWriter.Log($"Deserialized: {serializedInstanceRPCs.Length} RPCs.");
            return true;
        }

        public static bool TryWriteRPCStubs (string path, string serializedRPCs)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogWriter.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                MakeFileWriteable(path);
                File.WriteAllText(path, serializedRPCs);
            } catch (Exception exception)
            {
                LogWriter.LogError($"Unable to write RPC stubs JSON to path: \"{path}\", the following exception occurred.");
                LogWriter.LogException(exception);
                return false;
            }

            return true;
        }
    }
}
