using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public static class RPCSerializer
    {
        public static bool TryDeserializeType (string assemblyString, string typeString, out System.Type type)
        {
            type = null;

            if (!ReflectionUtils.TryGetAssemblyByName(assemblyString, out var assembly))
                return false;

            return (type = assembly.GetType(typeString)) != null;
        }

        private static Dictionary<System.Type, MethodInfo[]> cachedTypeMethodInfos = new Dictionary<System.Type, MethodInfo[]>();
        public static bool TryDeserializeMethodInfo (SerializedMethod method, out MethodInfo outMethodInfo)
        {
            outMethodInfo = null;

            if (!TryDeserializeType(method.declaringAssemblyName, method.declaryingTypeFullName, out var declaringType))
            {
                Debug.LogError($"Unable to find serialized method's declaring type: \"{method.declaryingTypeFullName}\" in assembly: \"{method.declaringAssemblyName}\".");
                return false;
            }

            if (!TryDeserializeType(method.declaringReturnTypeAssemblyName, method.returnTypeFullName, out var returnType))
            {
                Debug.LogError($"Unable to find serialized method's return type: \"{method.returnTypeFullName}\" in assembly: \"{method.declaringReturnTypeAssemblyName}\".");
                return false;
            }

            var parameterList = new List<(System.Type parameterType, string parameterName)>();
            if (method.ParameterCount > 0)
            {
                for (int i = 0; i < method.ParameterCount; i++)
                {
                    var parameterString = method[i];
                    if (!TryDeserializeType(parameterString.declaringParameterTypeAssemblyName, parameterString.parameterTypeFullName, out var parameterType))
                    {
                        Debug.LogError($"Unable to find serialize method's parameter type: \"{parameterString.parameterTypeFullName}\" in assembly: \"{parameterString.declaringParameterTypeAssemblyName}\".");
                        return false;
                    }

                    parameterList.Add((parameterType, parameterString.parameterName));
                }
            }

            MethodInfo[] methods = null;

            if (!cachedTypeMethodInfos.TryGetValue(declaringType, out methods))
            {
                methods = declaringType.GetMethods((method.isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public);
                cachedTypeMethodInfos.Add(declaringType, methods);
            }

            IEnumerable<MethodInfo> filteredMethods = methods.Where(methodInfo => methodInfo.ReturnType == returnType);
            var matchingNameMethods = filteredMethods.Where(methodInfo => methodInfo.Name == method.methodName);
            if (parameterList.Count > 0)
            {
                filteredMethods = filteredMethods.Where(methodInfo =>
                {
                    var parameters = methodInfo.GetParameters();
                    if (parameters.Length != parameterList.Count)
                        return false;

                    bool allMatching = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        allMatching &=
                            parameters[i].ParameterType == parameterList[i].parameterType &&
                            parameters[i].Name == parameterList[i].parameterName;
                    }

                    return allMatching;
                });
            }

            return (outMethodInfo = filteredMethods.FirstOrDefault()) != null;
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

        public static bool TryReadRegisteredAssemblies (string path, out string[] registeredAssemblyFullNames)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to read registered assemblies, the path is invalid.");
                registeredAssemblyFullNames = null;
                return false;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Unable to read registered assemblies from path: \"{path}\", the file does not exist.");
                registeredAssemblyFullNames = null;
                return false;
            }

            try
            {
                var assemblyFullNames = System.IO.File.ReadAllLines(path);
                registeredAssemblyFullNames = assemblyFullNames.Where(assemblyFullName => !string.IsNullOrEmpty(assemblyFullName)).ToArray();

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to read registered assemblies from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                registeredAssemblyFullNames = null;
                return false;
            }

            // Debug.Log($"Registered assemblies was read from path: \"{path}\".");
            return true;
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
            if (!TryReadRegisteredAssemblies(path, out string[] registeredAssemblyFullNames))
            {
                registeredAssemblies = null;
                return false;
            }

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

        public static bool TryReadRPCStubs (string path, out SerializedRPC[] serializedInstanceRPCs, out SerializedMethod[] stagedMethods)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to read RPC stubs, the path is invalid.");
                serializedInstanceRPCs = null;
                stagedMethods = null;
                return false;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
                serializedInstanceRPCs = null;
                stagedMethods = null;
                return false;
            }

            try
            {
                var text = System.IO.File.ReadAllText(path);
                if (!TryDeserializeRPCStubsJson(text, out serializedInstanceRPCs, out stagedMethods))
                    return false;

            }

            catch (System.Exception exception)
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                serializedInstanceRPCs = null;
                stagedMethods = null;
                return false;
            }


            // Debug.Log($"RPC Stubs was read from path: \"{path}\".");
            return true;
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
