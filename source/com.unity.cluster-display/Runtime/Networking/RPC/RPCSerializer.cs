using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class RPCSerializer
    {
        public struct RPCTokenizer
        {
            private string sourceString;
            public string Source => sourceString;

            private ushort rpcId;
            public ushort RPCId => rpcId;

            private bool isStatic;
            public bool IsStatic => isStatic;

            private string declaringAssemblyString;
            public string DeclaringAssemblyName => declaringAssemblyString;

            private string declaringTypeString;
            public string DeclaringTypeFullName => declaringTypeString;

            private string declaringReturnTypeAssemblyString;
            public string DeclaringReturnTypeAssemblyName => declaringReturnTypeAssemblyString;

            private string returnTypeString;
            public string ReturnTypeFullName => returnTypeString;

            private string methodNameString;
            public string MethodName => methodNameString;

            private readonly string[] declaringParameterTypeAssemblyStrings;
            private readonly string[] parameterTypeStrings;
            private readonly string[] parameterNameStrings;

            public (string declaringParameterTypeAssemblyName, string parameterTypeFullName, string parameterName) this[int parameterIndex]
            {
                get =>
                    parameterTypeStrings != null ? (
                        declaringParameterTypeAssemblyStrings[parameterIndex],
                        parameterTypeStrings[parameterIndex], 
                        parameterNameStrings[parameterIndex]) 

                    : (null, null, null);
            }
            public int ParameterCount => parameterTypeStrings != null ? parameterTypeStrings.Length : 0;

            public bool IsValid => !string.IsNullOrEmpty(sourceString);

            private static bool TryParseAssemblyAndType (ref string str, out string declaringAssemblyString, out string typeString)
            {
                declaringAssemblyString = null;
                typeString = null;

                if (string.IsNullOrEmpty(str))
                    return false;

                var assemblyAndTypeSplit = str.Split(':');
                if (assemblyAndTypeSplit.Length != 2)
                    return false;

                declaringAssemblyString = assemblyAndTypeSplit[0];
                if (string.IsNullOrEmpty(declaringAssemblyString))
                    return false;

                typeString = assemblyAndTypeSplit[1];
                if (string.IsNullOrEmpty(typeString))
                    return false;

                return true;
            }

            public RPCTokenizer (string source)
            {
                if (string.IsNullOrEmpty(source))
                    goto failure;

                var split = source.Split('|');

                var rpcIdStr = split[0];
                rpcId = 0;
                if (string.IsNullOrEmpty(rpcIdStr) && !ushort.TryParse(rpcIdStr, out rpcId))
                    goto failure;

                var instanceOrStatic = split[1];
                isStatic = instanceOrStatic == "static";

                var declaringAssemblyAndTypeStr = split[2];

                if (!TryParseAssemblyAndType(ref declaringAssemblyAndTypeStr, out declaringAssemblyString, out declaringTypeString))
                    goto failure;

                var returnAssemblyAndType = split[3];
                if (!TryParseAssemblyAndType(ref returnAssemblyAndType, out declaringReturnTypeAssemblyString, out returnTypeString))
                    goto failure;

                methodNameString = split[4];

                if (string.IsNullOrEmpty(methodNameString))
                    goto failure;

                var declaringParameterTypeAssemblyStringList = new List<string>();
                var paramterTypeStringList = new List<string>();
                var paramterNameStringList = new List<string>();

                string parametersString = split[5];
                if (!string.IsNullOrEmpty(parametersString) && parametersString != "void")
                {
                    var parameterSplit = parametersString.Split(',');

                    for (int i = 0; i < parameterSplit.Length; i++)
                    {
                        var parameterTypeAndNameSplit = parameterSplit[i].Split(' ');
                        if (parameterTypeAndNameSplit.Length != 2)
                            goto failure;

                        var parameterAssemblyAndTypeString = parameterTypeAndNameSplit[0];
                        if (!TryParseAssemblyAndType(ref parameterAssemblyAndTypeString, out var parameterAssemblyString, out var parameterTypeString))
                            goto failure;

                        var parameterName = parameterTypeAndNameSplit[1];
                        if (string.IsNullOrEmpty(parameterName))
                            goto failure;

                        declaringParameterTypeAssemblyStringList.Add(parameterAssemblyString);
                        paramterTypeStringList.Add(parameterTypeString);
                        paramterNameStringList.Add(parameterName);
                    }
                }

                if (paramterNameStringList.Count == 0)
                {
                    this.declaringParameterTypeAssemblyStrings = null;
                    this.parameterTypeStrings = null;
                    this.parameterNameStrings = null;
                }

                else
                {
                    this.declaringParameterTypeAssemblyStrings = declaringParameterTypeAssemblyStringList.ToArray();
                    this.parameterTypeStrings = paramterTypeStringList.ToArray();
                    this.parameterNameStrings = paramterNameStringList.ToArray();
                }

                this.sourceString = source;
                return;

                failure:
                this.sourceString = null;
                this.rpcId = 0;
                this.isStatic = false;

                this.declaringAssemblyString = null;
                this.declaringTypeString = null;

                this.declaringReturnTypeAssemblyString = null;
                this.returnTypeString = null;

                this.methodNameString = null;

                this.declaringParameterTypeAssemblyStrings = null;
                this.parameterTypeStrings = null;
                this.parameterNameStrings = null;
            }
        }

        public static bool TryDeserializeType (string assemblyString, string typeString, out System.Type type)
        {
            type = null;

            if (!ReflectionUtils.TryGetAssemblyByName(assemblyString, out var assembly))
                return false;

            return (type = assembly.GetType(typeString)) != null;
        }

        private static Dictionary<System.Type, MethodInfo[]> cachedTypeMethodInfos = new Dictionary<System.Type, MethodInfo[]>();
        public static bool TryDeserializeMethodInfo (string serializedMethod, out MethodInfo outMethodInfo)
        {
            var rpcTokenizer = new RPCTokenizer(serializedMethod);
            outMethodInfo = null;

            if (!rpcTokenizer.IsValid)
                return false;

            if (!TryDeserializeType(rpcTokenizer.DeclaringAssemblyName, rpcTokenizer.DeclaringTypeFullName, out var declaringType))
                return false;

            if (!TryDeserializeType(rpcTokenizer.DeclaringReturnTypeAssemblyName, rpcTokenizer.ReturnTypeFullName, out var returnType))
                return false;

            var parameterList = new List<(System.Type parameterType, string parameterName)>();
            if (rpcTokenizer.ParameterCount > 0)
            {
                for (int i = 0; i < rpcTokenizer.ParameterCount; i++)
                {
                    var parameterString = rpcTokenizer[i];
                    if (!TryDeserializeType(parameterString.declaringParameterTypeAssemblyName, parameterString.parameterTypeFullName, out var parameterType))
                        return false;

                    parameterList.Add((parameterType, parameterString.parameterName));
                }
            }

            MethodInfo[] methods = null;

            if (!cachedTypeMethodInfos.TryGetValue(declaringType, out methods))
            {
                methods = declaringType.GetMethods((rpcTokenizer.IsStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public);
                cachedTypeMethodInfos.Add(declaringType, methods);
            }

            IEnumerable<MethodInfo> filteredMethods = methods.Where(methodInfo => methodInfo.ReturnType == returnType);
            var matchingNameMethods = filteredMethods.Where(methodInfo => methodInfo.Name == rpcTokenizer.MethodName);
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

        public static bool TryReadRPCStubs (string path, out string[] lines)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to read RPC stubs, the path is invalid.");
                lines = null;
                return false;
            }

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the file does not exist.");
                lines = null;
                return false;
            }

            try
            {
                lines = System.IO.File.ReadAllLines(path);

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to read RPC stubs from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                lines = null;
                return false;
            }

            Debug.Log($"RPC Stubs was read from path: \"{path}\".");
            return true;
        }

        public static bool TryWriteRPCStubs (string path, string[] lines)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Unable to write RPC stubs, the path is invalid.");
                return false;
            }

            try
            {
                System.IO.File.WriteAllLines(path, lines);

            } catch (System.Exception exception)
            {
                Debug.LogError($"Unable to write RPC stubs from path: \"{path}\", the following exception occurred.");
                Debug.LogException(exception);
                return false;
            }

            Debug.Log($"RPC Stubs was written to path: \"{path}\".");
            return true;
        }

        public static bool TrySerializeMethodInfo (ref RPCMethodInfo rpcMethodInfo, out string serializedRPCMethodInfo)
        {
            if (!rpcMethodInfo.IsValid)
            {
                serializedRPCMethodInfo = null;
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
                serializedRPCMethodInfo = 
                    $"{rpcMethodInfo.rpcId}|" +
                    $"{(rpcMethodInfo.IsStatic ? "static" : "instance")}|" +
                    $"{declaringAssemblystr}:{declaringTypeStr}|" +
                    $"{returnTypeAssemblyName}:{returnType.FullName}|" +
                    $"{rpcMethodInfo.methodInfo.Name}|" +
                    $"void|";
            }

            else
            {
                var returnType = rpcMethodInfo.methodInfo.ReturnType;
                var returnTypeAssemblyName = returnType.Assembly.GetName().Name;
                serializedRPCMethodInfo = 
                    $"{rpcMethodInfo.rpcId}|" +
                    $"{(rpcMethodInfo.IsStatic ? "static" : "instance")}|" +
                    $"{declaringAssemblystr}:{declaringTypeStr}|" +
                    $"{returnTypeAssemblyName}:{returnType.FullName}|" +
                    $"{rpcMethodInfo.methodInfo.Name}|" +
                    $"{string.Join(",", rpcMethodInfo.methodInfo.GetParameters().Select(parameter => $"{parameter.ParameterType.Assembly.GetName().Name}:{parameter.ParameterType.FullName} {parameter.Name}"))}|";
            }

            return true;
        }
    }
}
