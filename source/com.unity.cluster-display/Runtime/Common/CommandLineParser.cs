using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Unity.ClusterDisplay
{
    public static class CommandLineArguments
    {
        public static float? linesThickness => CommandLineParser.linesThickness.Defined ? CommandLineParser.linesThickness.Value : null;
        public static float? linesScale => CommandLineParser.linesThickness.Defined ? CommandLineParser.linesScale.Value : null;
        public static float? linesAngle => CommandLineParser.linesThickness.Defined ? CommandLineParser.linesAngle.Value : null;
        public static float? linesShiftSpeed => CommandLineParser.linesThickness.Defined ? CommandLineParser.linesShiftSpeed.Value : null;
        public static float? linesRotationSpeed => CommandLineParser.linesThickness.Defined ? CommandLineParser.linesRotationSpeed.Value : null;
    }

    internal static class CommandLineParser
    {

#if UNITY_EDITOR
        /// This attribute is used to search for a method in ClusterSyncEditorConfig so we
        /// can call it through reflection.
        [AttributeUsage(AttributeTargets.Method)]
        public class CommandLineInjectionMethodAttribute : Attribute { }
#endif

        /// <summary>
        ///  This delegate is used to override the parsing of an argument's parameters.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="argumentName">The name of the command line argument.</param>
        /// <param name="result">The output value.</param>
        /// <returns></returns>
        internal delegate bool TryParseDelegate<T>(string argumentName, out T result);

        /// <summary>
        /// For testing purposes, some command line argument names change. For example, we need to test both emitter and repeater
        /// command line arguments and this resolver helps us resolve argument names dynamically.
        /// </summary>
        /// <returns></returns>
        internal delegate string ArgumentNameResolver();

        /// <summary>
        /// This allows us to have array's of arguments abstractly.
        /// </summary>
        internal abstract class BaseArgument 
        {
            internal abstract void Reset();
        }

        internal abstract class BaseArgument<T> : BaseArgument
        {
            /// If this is true, this class will throw an error complaining
            /// that the argument is not defined to the user.
            public readonly bool k_Required; 

            /// <summary>
            /// For dynamically resolving the argument name instead of using m_ArgumentName.
            /// </summary>
            readonly ArgumentNameResolver argumentNameResolver; 
            readonly string m_ArgumentName;

            /// <summary>
            /// A switch that will either retrieve the argument using the resolver, or use the readonly
            /// argument name string set on construction.
            /// </summary>
            public string ArgumentName
            {
                get
                {
                    // If we have a name resolver, lets use that instead.
                    if (argumentNameResolver != null)
                    {
                        return argumentNameResolver();
                    }

                    return m_ArgumentName;
                }
            }

            protected bool m_Defined; // If the argument is defined and valid.
            protected bool m_Cached; // If we've already a parse.

            protected T m_Value;

            /// Each argument has a default parser that can be overriden.
            readonly TryParseDelegate<T> tryParse;

            public bool Defined => GenericCheck(tryParse);

            public T Value
            {
                get
                {
                    GenericCheck(tryParse);
                    return m_Value;
                }
            }

            public void SetValue(T value) => m_Value = value;

            protected abstract bool DefaultParser(string argumentName, out T parsedResult);

            internal bool GenericCheck(TryParseDelegate<T> tryParse)
            {
                if (!m_Cached) // We should only enter in here once.
                {
                    // Return true or false if the argument was defined and we successfully parsed it.
                    m_Defined = 
                        tryParse != null && 
                        tryParse(ArgumentName, out m_Value);

                    m_Cached = true;
                }

                return m_Defined; 
            }

            internal BaseArgument(string argumentName, bool required = false)
            {
                Reset();

                k_Required = required;
                m_ArgumentName = argumentName;

                m_Cached = false;
                m_Value = default(T);

                tryParse = DefaultParser;
            }

            internal BaseArgument(string argumentName, TryParseDelegate<T> tryParseDelegate, bool required = false)
            {
                Reset();

                k_Required = required;
                m_ArgumentName = argumentName;

                tryParse = tryParseDelegate;
            }

            internal BaseArgument(ArgumentNameResolver argumentNameResolverDelegate, TryParseDelegate<T> tryParseDelegate, bool required = false)
            {
                Reset();

                k_Required = required;

                tryParse = tryParseDelegate;
                argumentNameResolver = argumentNameResolverDelegate;
            }

            internal override void Reset ()
            {
                ClusterDebug.Log($"Resetting cache for: \"{ArgumentName}\" cluster display argument.");

                m_Cached = false;
                m_Value = default(T);
            }
        }

        internal class BoolArgument : BaseArgument<bool>
        {
            protected override bool DefaultParser(string argumentName, out bool parsedResult)
            {
                parsedResult = TryGetIndexOfNodeTypeArgument(ArgumentName, out var startIndex);

                if (!parsedResult)
                {
                    if (k_Required)
                        ClusterDebug.LogError($"There is no argument with name: \"{ArgumentName}\" specified.");

                    return false;
                }

                return true;
            }

            internal BoolArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal BoolArgument(string argumentName, TryParseDelegate<bool> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
        }

        internal class StringArgument : BaseArgument<string>
        {
            protected override bool DefaultParser(string argumentName, out string parsedResult) => TryParseStringArgument(ArgumentName, out parsedResult, k_Required);

            internal StringArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal StringArgument(string argumentName, TryParseDelegate<string> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
            internal StringArgument(ArgumentNameResolver argumentNameResolver, TryParseDelegate<string> tryParse, bool required = false) : base(argumentNameResolver, tryParse, required) {}

            public static implicit operator string(StringArgument argument) => !argument.Defined ? null : argument.Value;
        }

        internal class FloatArgument : BaseArgument<float>
        {
            protected override bool DefaultParser(string argumentName, out float parsedResult) => TryParseFloatArgument(ArgumentName, out parsedResult, required: k_Required);

            internal FloatArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal FloatArgument(string argumentName, TryParseDelegate<float> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
            internal FloatArgument(ArgumentNameResolver argumentNameResolver, TryParseDelegate<float> tryParse, bool required = false) : base(argumentNameResolver, tryParse, required) {}
        }

        internal class IntArgument : BaseArgument<int>
        {
            protected override bool DefaultParser(string argumentName, out int result) => TryParseIntArgument(ArgumentName, out result, k_Required);

            internal IntArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal IntArgument(string argumentName, TryParseDelegate<int> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
            internal IntArgument(ArgumentNameResolver argumentNameResolver, TryParseDelegate<int> tryParse, bool required = false) : base(argumentNameResolver, tryParse, required) {}
        }

        internal class ByteArgument : BaseArgument<byte>
        {
            protected override bool DefaultParser(string argumentName, out byte result) => TryParseByteArgument(ArgumentName, out result, k_Required);

            internal ByteArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal ByteArgument(string argumentName, TryParseDelegate<byte> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
            internal ByteArgument(ArgumentNameResolver argumentNameResolver, TryParseDelegate<byte> tryParse, bool required = false) : base(argumentNameResolver, tryParse, required) {}
        }

        internal class Vector2IntArgument : BaseArgument<Vector2Int>
        {
            protected override bool DefaultParser(string argumentName, out Vector2Int result) => TryParseVector2Int(ArgumentName, out result, k_Required);

            internal Vector2IntArgument(string argumentName, bool required = false) : base(argumentName, required) { }
            internal Vector2IntArgument(string argumentName, TryParseDelegate<Vector2Int> tryParse, bool required = false) : base(argumentName, tryParse, required) { }
            internal Vector2IntArgument(ArgumentNameResolver argumentNameResolver, TryParseDelegate<Vector2Int> tryParse, bool required = false) : base(argumentNameResolver, tryParse, required) {}
        }

        internal const string k_EmitterNodeTypeArgument = "-emitterNode";
        internal const string k_RepeaterNodeTypeArgument = "-node";

        internal static readonly BoolArgument debugFlag                     = new BoolArgument("-clusterNode");

        internal static readonly BoolArgument emitterSpecified              = new BoolArgument("-emitterNode");
        internal static readonly BoolArgument headlessEmitter               = new BoolArgument("-batchMode");
        internal static readonly BoolArgument replaceHeadlessEmitter        = new BoolArgument("-replaceHeadlessEmitter");

        internal static readonly BoolArgument repeaterSpecified             = new BoolArgument("-node");
        internal static readonly BoolArgument delayRepeaters                = new BoolArgument("-delayRepeaters");

        internal static readonly ByteArgument nodeID                        = new ByteArgument(GetNodeType, tryParse: ParseNodeID);
        internal readonly static IntArgument repeaterCount                  = new IntArgument(GetNodeType, tryParse: ParseRepeaterCount);

        internal static readonly Vector2IntArgument gridSize                = new Vector2IntArgument("-gridSize");
        internal static readonly Vector2IntArgument bezel                   = new Vector2IntArgument("-bezel");
        internal static readonly Vector2IntArgument physicalScreenSize      = new Vector2IntArgument("-physicalScreenSize");

        internal readonly static IntArgument targetFps                      = new IntArgument("-targetFps", tryParse: ParseTargetFPS);
        internal static readonly IntArgument overscan                       = new IntArgument("-overscan");
        internal static readonly IntArgument quadroSyncInitDelay            = new IntArgument("-quadroSyncInitDelay");

        internal static readonly FloatArgument linesThickness               = new FloatArgument("-linesThickness");
        internal static readonly FloatArgument linesScale                   = new FloatArgument("-linesScale");
        internal static readonly FloatArgument linesShiftSpeed              = new FloatArgument("-linesShiftSpeed");
        internal static readonly FloatArgument linesAngle                   = new FloatArgument("-linesAngle");
        internal static readonly FloatArgument linesRotationSpeed           = new FloatArgument("-linesRotationSpeed");

        internal static readonly StringArgument adapterName                 = new StringArgument("-adapterName");
        internal static readonly StringArgument multicastAddress            = new StringArgument(GetNodeType, tryParse: ParseMulticastAddress);
        internal static readonly IntArgument rxPort                         = new IntArgument(GetNodeType, ParseRXPort);
        internal static readonly IntArgument txPort                         = new IntArgument(GetNodeType, ParseTXPort);

        internal static readonly IntArgument handshakeTimeout               = new IntArgument("-handshakeTimeout");
        internal static readonly IntArgument communicationTimeout           = new IntArgument("-communicationTimeout");

        internal readonly static BaseArgument[] baseArguments = new BaseArgument[] 
        { 
            debugFlag,
            emitterSpecified,
            headlessEmitter,
            replaceHeadlessEmitter,
            repeaterSpecified,
            delayRepeaters,
            nodeID,
            repeaterCount,
            gridSize,
            bezel,
            physicalScreenSize,
            targetFps,
            overscan,
            quadroSyncInitDelay,
            linesThickness,
            linesScale,
            linesShiftSpeed,
            linesAngle,
            linesRotationSpeed,
            adapterName,
            multicastAddress,
            rxPort,
            txPort,
            handshakeTimeout,
            communicationTimeout
        };

        // Since this property is referenced by some arguments when this class is initialized, this will be one of the very first things called.
        private static string nodeType => Arguments.Contains(k_EmitterNodeTypeArgument) ? k_EmitterNodeTypeArgument : k_RepeaterNodeTypeArgument;

        /// <summary>
        /// We are using this as the argument name resolver.
        /// </summary>
        /// <returns></returns>
        private static string GetNodeType() => nodeType;

        internal static bool clusterDisplayLogicSpecified => emitterSpecified.Value || repeaterSpecified.Value;
        private static int addressStartOffset => (emitterSpecified.Value ? 3 : 2);

        internal static void CacheArguments (bool ? overrideIsEmitter = null)
        {
            Reset();

            m_Arguments = new List<string>(20) { };

            m_Arguments.AddRange(System.Environment.GetCommandLineArgs());

#if UNITY_EDITOR
            // This section of code is kind of a hack, but it's purpose is to retrieve the editor only scriptable
            // object: "ClusterSyncEditorConfig" and through reflection call a method to override the arguments
            // with the arguments stored in the scriptable object.
            var assets = AssetDatabase.FindAssets("t:ClusterSyncEditorConfig"); // Make sure that if you rename this class, you need to change that here as well.

            if (assets.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(assets[0]);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                var clusterSyncEditorConfig = AssetDatabase.LoadAssetAtPath(path, type);

                var privateStaticMethods = type.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                MethodInfo targetMethod = null;

                foreach (var method in privateStaticMethods)
                {
                    if (method.GetCustomAttribute<CommandLineInjectionMethodAttribute>() == null)
                    {
                        continue;
                    }

                    targetMethod = method;
                    break;
                }

                if (targetMethod == null)
                {
                    throw new Exception($"The type: \"ClusterSyncEditorConfig\" must contain a private static method with the attribute: \"{nameof(CommandLineInjectionMethodAttribute)} in order to inject the command line arguments from the cluster display manager.");
                }

                // Call ClusterSyncEditorConfig.PollArguments()
                var list = targetMethod.Invoke(clusterSyncEditorConfig, new object[1] { overrideIsEmitter }) as List<string>;

                Debug.Assert(list != null, "Received a NULL list while attempting to retrieve command line arguments from editor config");

                if (list.Count > 0)
                {
                    m_Arguments.AddRange(list);
                }
            }
#endif

            PrintArguments(m_Arguments);
        }

        private static List<string> m_Arguments;
        internal static List<string> Arguments
        {
            get
            {
                if (m_Arguments == null)
                {
                    CacheArguments();
                }

                return m_Arguments;
            }
        }

        private static void LogNoArgument (string argumentName) => ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

        internal static void Reset()
        {
            if (m_Arguments == null)
            {
                return;
            }

            ClusterDebug.Log("Resetting command line arguments.");

            m_Arguments.Clear();
            m_Arguments = null;

            for (int i = 0; i < baseArguments.Length; i++)
            {
                baseArguments[i].Reset();
            }
        }

        internal static void Override (List<string> arguments)
        {
            Reset();

            ClusterDebug.Log("Overriding command line arguments.");
            m_Arguments.AddRange(arguments);
        }

        private static void PrintArguments(List<string> arguments)
        {
            string msg = "Arguments:";

            for (int i = 0; i < arguments.Count; i++)
                msg = $"{msg}\n\t{arguments[i]}";

            ClusterDebug.Log(msg);
        }

        private static bool ParseNodeID (string argumentName, out byte nodeId)
        {
            nodeId = 0;

            if (!TryGetIndexOfNodeTypeArgument(nodeType, out var startIndex))
            {
                LogNoArgument(argumentName);
                return false;
            }

            ParseId(startIndex + 1, out nodeId);
            return true;
        }

        private static bool ParseMulticastAddress(string argumentName, out string address)
        {
            address = null;
            if (!TryGetIndexOfNodeTypeArgument(argumentName, out var startIndex))
            {
                LogNoArgument(argumentName);
                return false;
            }

            ParseMulticastAddressAndPort(startIndex + addressStartOffset, out address, out var rx, out var tx);
            rxPort.SetValue(rx);
            txPort.SetValue(tx);

            return true;
        }

        private static bool ParseRXPort(string argumentName, out int port)
        {
            port = -1;
            if (!TryParsePorts(out port, out var tx))
            {
                ClusterDebug.LogError($"Unable to parse RX port.");
                return false;
            }

            txPort.SetValue(tx);

            return true;
        }

        private static bool ParseTXPort(string argumentName, out int port)
        {
            port = -1;
            if (!TryParsePorts(out var rx, out port))
            {
                ClusterDebug.LogError($"Unable to parse TX port.");
                return false;
            }

            rxPort.SetValue(rx);

            return true;
        }

        private static bool ParseTargetFPS (string argumentName, out int targetFPS)
        {
            bool result = TryParseIntArgument(argumentName, out targetFPS, required: false);

            // We may be setting this result to something like Application.targetFrameRate down stream, therefore set it to -1 so the
            // FPS is unlimited if we get an invalid result:
            // https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html
            if (targetFPS <= 0)
                targetFPS = -1;

            return result;

        }

        private static bool ParseRepeaterCount (string argumentName, out int repeaterCount)
        {
            if (!TryGetIndexOfNodeTypeArgument(nodeType, out var startIndex))
            {
                repeaterCount = 0;
                return false;
            }

            ParseRepeaterCount(startIndex + 2, out repeaterCount);

            return true;
        }

        private static bool TryParsePorts(out int rxPort, out int txPort)
        {
            rxPort = -1;
            txPort = -1;
            if (!TryGetIndexOfNodeTypeArgument(nodeType, out var startIndex))
                return false;

            ParsePorts(startIndex + addressStartOffset, out rxPort, out txPort);

            return true;
        }

        internal static bool TryGetIndexOfNodeTypeArgument(string nodeType, out int indexOfNodeTypeArgument)
        {
            indexOfNodeTypeArgument = Arguments.IndexOf(nodeType);
            return indexOfNodeTypeArgument > -1;
        }

        private static bool ValidateStartIndex(int startIndex) =>
            startIndex > -1 && startIndex < Arguments.Count;

        private static void ParseId(int startIndex, out byte id)
        {
            if (!ValidateStartIndex(startIndex) || !byte.TryParse(Arguments[startIndex], out id))
                throw new Exception("Unable to parse ID argument.");
        }

        private static void ParseRepeaterCount(int startIndex, out int repeaterCount)
        {
            if (!ValidateStartIndex(startIndex) || !int.TryParse(Arguments[startIndex], out repeaterCount))
                throw new Exception("Unable to parse repeater count argument.");
        }

        private static void ParseMulticastAddress(int startIndex, out string multicastAddress)
        {
            if (!ValidateStartIndex(startIndex))
                throw new Exception(
                    "Missing multicast address and RX+TX port as arguments, format should be: (multicast address):(rx port),(tx port)");

            int colonIndex = Arguments[startIndex].IndexOf(':');
            if (colonIndex == -1)
                throw new Exception(
                    "Unable to parse multicast address, the port separator: \":\" is missing. Format should be: (multicast address):(rx port),(tx port)");

            multicastAddress = Arguments[startIndex].Substring(0, colonIndex);
            if (!ValidateStartIndex(startIndex) || string.IsNullOrEmpty(multicastAddress))
                throw new Exception(
                    $"Unable to parse multicast address: \"{multicastAddress}\", format should be: (multicast address):(rx port),(tx port)");
        }

        private static void ParsePorts(int startIndex, out int rxPort, out int txPort)
        {
            rxPort = -1;
            txPort = -1;

            if (!ValidateStartIndex(startIndex))
                throw new Exception(
                    "Unable to parse RX and TX ports, format should be: (multicast address):(rx port),(tx port)");

            int colonIndex = Arguments[startIndex].IndexOf(':');
            if (colonIndex == -1)
                throw new Exception(
                    "Unable to parse RX and TX ports, the port separator: \":\" is missing. Format should be: (multicast address):(rx port),(tx port)");

            string ports = Arguments[startIndex].Substring(colonIndex + 1);

            int indexOfSeparator = ports.IndexOf(',');
            if (indexOfSeparator == -1)
                throw new Exception(
                    "RX and TX port separator: \",\" is missing, format should be: (ip address):(rx port),(tx port)");

            if (indexOfSeparator + 1 >= ports.Length)
                throw new Exception(
                    "Unable to parse RX and TX port argument, format should be: (ip address):(rx port),(tx port)");

            string rxPortStr = ports.Substring(0, indexOfSeparator);
            if (!int.TryParse(rxPortStr, out rxPort))
                throw new Exception($"RX port argument: \"{rxPortStr}\" is invalid.");

            string txPortStr = ports.Substring(indexOfSeparator + 1);
            if (!int.TryParse(txPortStr, out txPort))
                throw new Exception($"TX port argument: \"{rxPortStr}\" is invalid.");
        }

        private static void ParseMulticastAddressAndPort(int startIndex, out string multicastAddress, out int rxPort,
            out int txPort)
        {
            ParseMulticastAddress(startIndex, out multicastAddress);
            ParsePorts(startIndex, out rxPort, out txPort);
        }

        internal static bool TryParseVector2Int(string argumentName, out Vector2Int value, bool required = false)
        {
            value = Vector2Int.zero;
            if (!TryParseStringArgument(argumentName, out var str, required: required))
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                return false;
            }

            var split = str.ToLower().Split('x');
            if (split == null || split.Length != 2)
                return false;

            if (!int.TryParse(split[0], out var x) || !int.TryParse(split[1], out var y))
            {
                ClusterDebug.LogError($"Unable to parse Vector2Int argument with name: \"{argumentName}\", it's value: \"{str}\" cannot be parsed as an Vector2Int in format!");
                return false;
            }

            value.x = x;
            value.y = y;
            return true;
        }

        internal static bool TryParseStringArgument (string argumentName, out string argumentValue, bool required = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = null;
                return false;
            }

            argumentValue = Arguments[startIndex + 1];
            return argumentValue != null;
        }

        internal static bool TryParseFloatArgument (string argumentName, out float argumentValue, bool required = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = 0;
                return false;
            }

            string argumentValueStr = Arguments[startIndex + 1];
            if (!float.TryParse(argumentValueStr, out argumentValue))
            {
                ClusterDebug.LogError($"Unable to parse int argument with name: \"{argumentName}\", it's value: \"{argumentValue}\" cannot be parsed as an int!");
                argumentValue = 0;
                return false;
            }

            return true;
        }

        internal static bool TryParseByteArgument (string argumentName, out byte argumentValue, bool required = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = 0;
                return false;
            }

            string argumentValueStr = Arguments[startIndex + 1];
            if (!byte.TryParse(argumentValueStr, out argumentValue))
            {
                ClusterDebug.LogError($"Unable to parse int argument with name: \"{argumentName}\", it's value: \"{argumentValue}\" cannot be parsed as an int!");
                argumentValue = 0;
                return false;
            }

            return true;
        }

        internal static bool TryParseIntArgument (string argumentName, out int argumentValue, bool required = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = 0;
                return false;
            }

            string argumentValueStr = Arguments[startIndex + 1];
            if (!int.TryParse(argumentValueStr, out argumentValue))
            {
                ClusterDebug.LogError($"Unable to parse int argument with name: \"{argumentName}\", it's value: \"{argumentValue}\" cannot be parsed as an int!");
                argumentValue = 0;
                return false;
            }

            return true;
        }

        internal static bool TryParseAddressAndPort (string argumentName, out string address, out int port, bool required = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (required)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                address = null;
                port = -1;
                return false;
            }

            string argumentValueStr = Arguments[startIndex + 1];
            int colonSeparatorIndex = argumentValueStr.IndexOf(':');
            if (colonSeparatorIndex == -1)
            {
                ClusterDebug.LogError($"Unable to parse address argument with name: \"{argumentName}\", the address format: \"{argumentValueStr}\" is malformed. It should be formatted in the following way: {argumentName} (address):(port)");
                address = null;
                port = -1;
                return false;
            }

            address = argumentValueStr.Substring(0, colonSeparatorIndex);
            if (string.IsNullOrEmpty(address))
            {
                ClusterDebug.LogError($"Unable to parse address argument with name: \"{argumentName}\" and value: \"{argumentValueStr}\", the address is missing and should be formatted in the following way: {argumentName} (address):(port)");
                address = null;
                port = -1;
                return false;
            }

            var portStr = argumentValueStr.Substring(colonSeparatorIndex + 1, argumentValueStr.Length - colonSeparatorIndex - 1);
            if (!int.TryParse(portStr, out port))
            {
                ClusterDebug.LogError($"Unable to parse address:port argument with name: \"{argumentName}\". The port argument: \"{portStr}\" in: \"{argumentValueStr}\" cannot be parsed as an int.");
                address = null;
                port = -1;
                return false;
            }

            return true;
        }
    }
}