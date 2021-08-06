using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public static class RPCSerializer
    {
        public static bool TryDeserializeType (string assemblyString, string namespaceString, string typeString, out System.Type type)
        {
            type = null;
            if (string.IsNullOrEmpty(assemblyString) || string.IsNullOrEmpty(typeString))
            {
                Debug.LogError("Unable to deserialize type, either the assembly or type string is null or invalid!");
                return false;
            }

            if (!ReflectionUtils.TryGetAssemblyByName(assemblyString, out var assembly))
                return false;

            if (!string.IsNullOrEmpty(namespaceString))
            {
                var nestedSplit = namespaceString.Split('/'); // Nested types are embedded in the namespace and separated by the "/" character.

                if (nestedSplit.Length > 1) // Type is nested, so we need to walk up the nesting hierarchy finding types.
                {
                    var containerTypes = new System.Type[nestedSplit.Length - 1];

                    var nestedHierarchy = namespaceString;
                    namespaceString = nestedSplit[0]; // The first element should always be the namespace.

                    containerTypes[0] = assembly.GetType(!string.IsNullOrEmpty(namespaceString) ? $"{namespaceString}.{nestedSplit[1]}" : nestedSplit[1]);
                    if (containerTypes[0] == null)
                    {
                        Debug.LogError($"Unable to find nested type: \"{typeString}\", cannot find the root type: \"{nestedSplit[1]}\" in our nested type hierarchy: \"{nestedHierarchy}\".");
                        return false;
                    }

                    for (int i = 0; i < containerTypes.Length; i++)
                    {
                        ReflectionUtils.ForeachNestedType(containerTypes[i], (nestedType) =>
                        {
                            if (nestedType.Name != nestedSplit[i + 1])
                                return true;

                            containerTypes[i] = nestedType;
                            return false;
                        });

                        if (containerTypes[i] == null)
                        {
                            Debug.LogError($"Unable to find nested type: \"{typeString}\", cannot find container type: \"{nestedSplit[i + 1]} from nested type hierarchy: \"{nestedHierarchy}\".");
                            return false;
                        }
                    }

                    type = containerTypes[containerTypes.Length - 1];
                    return true;
                }

                return (type = assembly.GetType( $"{namespaceString}.{typeString}")) != null;
            }

            return (type = assembly.GetType(typeString)) != null;
        }

        private static Dictionary<System.Type, MethodInfo[]> cachedTypeMethodInfos = new Dictionary<System.Type, MethodInfo[]>();
        public static bool TryDeserializeMethodInfo (SerializedMethod method, out MethodInfo outMethodInfo)
        {
            outMethodInfo = null;

            if (!TryDeserializeType(method.declaringAssemblyName, method.declaringTypeNamespace, method.declaryingTypeName, out var declaringType))
            {
                Debug.LogError($"Unable to find serialized method's declaring type: \"{method.declaryingTypeName}\" in assembly: \"{method.declaringAssemblyName}\".");
                return false;
            }

            if (!TryDeserializeType(method.declaringReturnTypeAssemblyName, method.returnTypeNamespace, method.returnTypeName, out var returnType))
            {
                Debug.LogError($"Unable to find serialized method's return type: \"{method.returnTypeName}\" in assembly: \"{method.declaringReturnTypeAssemblyName}\".");
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
                        Debug.LogError($"Unable to find serialize method's parameter type: \"{parameterString.parameterTypeName}\" in assembly: \"{parameterString.declaringParameterTypeAssemblyName}\".");
                        return false;
                    }

                    parameterList.Add((parameterType, parameterString.parameterName));
                }
            }

            MethodInfo[] methods = null;

            if (!cachedTypeMethodInfos.TryGetValue(declaringType, out methods))
            {
                methods = declaringType.GetMethods((method.isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public | BindingFlags.NonPublic);
                cachedTypeMethodInfos.Add(declaringType, methods);
            }

            IEnumerable<MethodInfo> filteredMethods = methods.Where(methodInfo =>
            {
                bool match = methodInfo.ReturnType == returnType;
                return match;
            });

            var matchingNameMethods = filteredMethods.Where(methodInfo => methodInfo.Name == method.methodName);
            if (parameterList.Count > 0)
            {
                filteredMethods = matchingNameMethods.Where(methodInfo =>
                {
                    var parameters = methodInfo.GetParameters();
                    if (parameters.Length != parameterList.Count)
                        return false;

                    bool allMatching = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType.IsGenericType)
                            allMatching &=
                                parameters[i].ParameterType.GetGenericTypeDefinition() == parameterList[i].parameterType &&
                                parameters[i].Name == parameterList[i].parameterName;

                        else allMatching &=
                            parameters[i].ParameterType == parameterList[i].parameterType &&
                            parameters[i].Name == parameterList[i].parameterName;
                    }

                    return allMatching;
                });
            }

            var foundMethod = filteredMethods.FirstOrDefault();
            if (foundMethod == null)
            {
                Debug.LogError($"Unable to deserialize method: \"{method.methodName}\", declared in type: \"{method.declaryingTypeName}\", if the method has renamed, you can use the {nameof(ClusterRPC)} attribute with the formarlySerializedAs parameter to insure that the method is deserialized properly.");
                return false;
            }

            if (!foundMethod.IsPublic)
            {
                Debug.LogError($"Unable to use method deserialized method: \"{foundMethod.Name}\" declared in type: \"{(string.IsNullOrEmpty(foundMethod.DeclaringType.Namespace) ? foundMethod.DeclaringType.Name  : $"{foundMethod.DeclaringType.Namespace}.{foundMethod.DeclaringType.Name}")}\", the method must be public.");
                return false;
            }

            outMethodInfo = foundMethod;
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

        public static bool TryWriteRegisteredAssemblies (string path, string serializedAssemblies)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                System.IO.File.WriteAllText(path, serializedAssemblies);

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to write registered assemblies to path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                return false;
            }

            // Debug.Log($"RPC Stubs was written to path: \"{path}\".");
            return true;
        }

        public static void ReadRegisteredAssemblies (string path, out string[] registeredAssemblyFullNames)
        {
            registeredAssemblyFullNames = new string[0];
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Unable to read registered assemblies, the path is invalid.");
                return;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"Unable to read registered assemblies from path: \"{path}\", the file does not exist.");
                return;
            }

            try
            {
                var assemblyFullNames = System.IO.File.ReadAllLines(path);
                registeredAssemblyFullNames = assemblyFullNames.Where(assemblyFullName => !string.IsNullOrEmpty(assemblyFullName)).ToArray();

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to read registered assemblies from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
            }

            // Debug.Log($"Registered assemblies was read from path: \"{path}\".");
            return;
        }

        public static bool TryDeserializeRegisteredAssemblies (string serializedRegisteredAssemblies, out List<Assembly> registeredAssemblies)
        {
            if (string.IsNullOrEmpty(serializedRegisteredAssemblies))
            {
                Debug.LogError("String containing serialized registered assemblies is null or empty.");
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

            // Debug.Log($"Deserialized: {registeredAssemblies.Count} registered assemblies.");
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

                jsonString = JsonUtility.ToJson(serializedInstanceRPCs, true);
            }

            catch (System.Exception exception)
            {
                Debug.LogError($"Unable to serialize RPC stubs, the following exception occurred.");
                Debug.LogException(exception);
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
                Debug.LogError("Unable to read RPC stubs, the path is invalid.");
                return;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
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
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                return;
            }
        }

        public static bool TryDeserializeRPCStubsJson (string jsonStr, out SerializedRPC[] rpcs, out SerializedMethod[] stagedMethods)
        {
            try
            {
                var stubs = JsonUtility.FromJson<RPCStubs>(jsonStr);
                rpcs = stubs.rpcs;
                stagedMethods = stubs.stagedMethods;

            }

            catch (System.Exception exception)
            {
                Debug.LogError($"Unable to parse RPC stubs JSON, the following exception occurred.");
                Debug.LogException(exception);
                rpcs = null;
                stagedMethods = null;
                return false;
            }

            // Debug.Log($"Deserialized: {serializedInstanceRPCs.Length} RPCs.");
            return true;
        }

        public static bool TryWriteRPCStubs (string path, string serializedRPCs)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                System.IO.File.WriteAllText(path, serializedRPCs);
            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to write RPC stubs JSON to path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                return false;
            }

            return true;
        }
    }
}
