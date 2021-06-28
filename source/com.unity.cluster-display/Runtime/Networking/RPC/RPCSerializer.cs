using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
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
        public static bool TryDeserializeMethodInfo (SerializedRPC rpcTokenizer, out RPCExecutionStage rpcExecutionStage, out MethodInfo outMethodInfo)
        {
            rpcExecutionStage = (RPCExecutionStage)rpcTokenizer.rpcExecutionStage;
            outMethodInfo = null;

            if (!TryDeserializeType(rpcTokenizer.declaringAssemblyName, rpcTokenizer.declaryingTypeFullName, out var declaringType))
            {
                Debug.LogError($"Unable to find serialized method's declaring type: \"{rpcTokenizer.declaryingTypeFullName}\" in assembly: \"{rpcTokenizer.declaringAssemblyName}\".");
                return false;
            }

            if (!TryDeserializeType(rpcTokenizer.declaringReturnTypeAssemblyName, rpcTokenizer.returnTypeFullName, out var returnType))
            {
                Debug.LogError($"Unable to find serialized method's return type: \"{rpcTokenizer.returnTypeFullName}\" in assembly: \"{rpcTokenizer.declaringReturnTypeAssemblyName}\".");
                return false;
            }

            var parameterList = new List<(System.Type parameterType, string parameterName)>();
            if (rpcTokenizer.ParameterCount > 0)
            {
                for (int i = 0; i < rpcTokenizer.ParameterCount; i++)
                {
                    var parameterString = rpcTokenizer[i];
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
                methods = declaringType.GetMethods((rpcTokenizer.isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public);
                cachedTypeMethodInfos.Add(declaringType, methods);
            }

            IEnumerable<MethodInfo> filteredMethods = methods.Where(methodInfo => methodInfo.ReturnType == returnType);
            var matchingNameMethods = filteredMethods.Where(methodInfo => methodInfo.Name == rpcTokenizer.methodName);
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

        public static bool TryWriteRegisteredAssemblies (string path, string[] assemblies)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                System.IO.File.WriteAllLines(path, assemblies);

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

            Debug.Log($"Registered assemblies was read from path: \"{path}\".");
            return true;
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

        public static bool TryReadRPCStubs (string path, out SerializedRPC[] serializedInstanceRPCs)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to read RPC stubs, the path is invalid.");
                serializedInstanceRPCs = null;
                return false;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
                serializedInstanceRPCs = null;
                return false;
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                serializedInstanceRPCs = JsonUtility.FromJson<SerializedRPCs>(json).rpcs;

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                serializedInstanceRPCs = null;
                return false;
            }

            Debug.Log($"RPC Stubs was read from path: \"{path}\".");
            return true;
        }

        public static bool TryWriteRPCStubs (string path, SerializedRPC[] lines)
        {
            var serializedInstanceRPCs = new SerializedRPCs
            {
                rpcs = lines
            };

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(serializedInstanceRPCs, true);
                System.IO.File.WriteAllText(path, json);

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to write RPC stubs to path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                return false;
            }

            // Debug.Log($"RPC Stubs was written to path: \"{path}\".");
            return true;
        }

        public static bool TryCreateSerializableRPC (ref RPCMethodInfo rpcMethodInfo, out SerializedRPC serializedRPCMethodInfo)
        {
            if (!rpcMethodInfo.IsValid)
            {
                serializedRPCMethodInfo = default(SerializedRPC);
                return false;
            }

            var declaringType = rpcMethodInfo.methodInfo.DeclaringType;
            var declaringAssembly = declaringType.Assembly;

            string declaringTypeStr = declaringType.FullName;
            string declaringAssemblystr = declaringAssembly.GetName().Name;

            var parameters = rpcMethodInfo.methodInfo.GetParameters();

            if (parameters.Length == 0)
            {
                var returnType = rpcMethodInfo.methodInfo.ReturnType;
                var returnTypeAssemblyName = returnType.Assembly.GetName().Name;
                serializedRPCMethodInfo = new SerializedRPC
                {
                    rpcId = rpcMethodInfo.rpcId,
                    isStatic = rpcMethodInfo.IsStatic,
                    rpcExecutionStage = (int)rpcMethodInfo.rpcExecutionStage,
                    declaringAssemblyName = declaringAssemblystr,
                    declaryingTypeFullName = declaringTypeStr,
                    declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                    returnTypeFullName = returnType.FullName,
                    methodName = rpcMethodInfo.methodInfo.Name,
                    declaringParameterTypeAssemblyNames = new string[0],
                    parameterTypeFullNames = new string[0],
                    parameterNames = new string[0],
                };
            }

            else
            {
                var returnType = rpcMethodInfo.methodInfo.ReturnType;
                var returnTypeAssemblyName = returnType.Assembly.GetName().Name;

                var parameterTypeAssemblyNames = new string[parameters.Length];
                var parameterTypeNames = new string[parameters.Length];
                var parameterNames = new string[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    parameterTypeAssemblyNames[i] = parameters[i].ParameterType.Assembly.GetName().Name;
                    parameterTypeNames[i] = parameters[i].ParameterType.FullName;
                    parameterNames[i] = parameters[i].Name;
                }

                serializedRPCMethodInfo = new SerializedRPC
                {
                    rpcId = rpcMethodInfo.rpcId,
                    isStatic = rpcMethodInfo.IsStatic,
                    rpcExecutionStage = (int)rpcMethodInfo.rpcExecutionStage,
                    declaringAssemblyName = declaringAssemblystr,
                    declaryingTypeFullName = declaringTypeStr,
                    declaringReturnTypeAssemblyName = returnTypeAssemblyName,
                    returnTypeFullName = returnType.FullName,
                    methodName = rpcMethodInfo.methodInfo.Name,
                    declaringParameterTypeAssemblyNames = parameterTypeAssemblyNames,
                    parameterTypeFullNames = parameterTypeNames,
                    parameterNames = parameterNames,
                };
            }

            return true;
        }
    }
}
