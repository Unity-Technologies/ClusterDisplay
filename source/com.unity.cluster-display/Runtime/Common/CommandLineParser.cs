using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Helpers")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Graphics")]

namespace Unity.ClusterDisplay
{
    internal static class CommandLineParser
    {
        internal static void OverrideArguments(List<string> overridingArguments)
        {
            ResetCache();
            PrintArguments(overridingArguments);
            m_Arguments = overridingArguments;   
        }

        private static void ResetCache()
        {
            m_Arguments = null;

            m_HeadlessEmitter = null;
            m_RepeaterReplacesHeadlessEmitter = null;
            m_ClusterLogicSpecified = null;
            m_NodeTypeStr = null;
            m_NodeID = null;
            m_MulticastAddress = null;
            m_RXPort = null;
            m_TXPort = null;
            m_AdapterName = null;
            m_RepeaterCount = null;
            m_DebugFlag = null;
            m_EmitterSpecified = null;
            m_RepeaterSpecified = null;
            m_TargetFPS = null;
            m_GridSize = null;
            m_Bezel = null;
            m_PhysicalScreenSize = null;
        }

        private static void PrintArguments(List<string> arguments)
        {
            string msg = "Arguments:";

            for (int i = 0; i < arguments.Count; i++)
                msg = $"{msg}\n\t{arguments[i]}";
            
            ClusterDebug.Log(msg);
        }
        
        #if UNITY_EDITOR
        public delegate void OnPollArguments (List<string> arguments);
        public static OnPollArguments onPollArguments;
        #endif

        private static List<string> m_Arguments;
        internal static List<string> Arguments
        {
            get
            {
                if (m_Arguments == null)
                {
                    m_Arguments = new List<string>(20) {};
                    m_Arguments.AddRange(System.Environment.GetCommandLineArgs());

                    #if UNITY_EDITOR
                    onPollArguments?.Invoke(m_Arguments);
                    #endif

                    PrintArguments(m_Arguments);
                }
                
                return m_Arguments;
            }
        }

        internal const string k_EmitterNodeTypeArgument = "-emitterNode";
        internal const string k_HeadlessEmitterArgument = "-batchMode";
        internal const string k_RepeaterReplacesHeadlessEmitterArgument = "-replaceHeadlessEmitter";
        internal const string k_RepeaterNodeTypeArgument = "-node";
        internal const string k_DebugArgument = "-clusterNode";
        internal const string k_GridSize = "-gridSize";
        internal const string k_Bezel = "-bezel";
        internal const string k_Overscan = "-overscan";
        internal const string k_PhysicalScreenSize = "-physicalScreenSize";
        internal const string k_AdapterNameArgument = "-adapterName";
        internal const string k_HandShakeTimeoutArgument = "-handshakeTimeoutArgument";
        internal const string k_CommunicationTimeoutArgument = "-communicationTimeoutArgument";
        internal const string k_TargetFPS = "-targetFps";

        private static bool? m_HeadlessEmitter;
        private static bool? m_RepeaterReplacesHeadlessEmitter;
        private static bool? m_ClusterLogicSpecified;
        private static string m_NodeTypeStr;
        private static byte? m_NodeID;
        private static string m_MulticastAddress;
        private static int ? m_RXPort;
        private static int ? m_TXPort;
        private static string m_AdapterName;
        private static int? m_RepeaterCount;
        private static bool? m_DebugFlag;
        private static bool? m_EmitterSpecified;
        private static bool? m_RepeaterSpecified;
        private static int? m_TargetFPS;
        private static Vector2Int? m_GridSize;
        private static Vector2Int? m_Bezel;
        private static int? m_Overscan;
        private static Vector2Int? m_PhysicalScreenSize;
        
        private static string nodeTypeStr
        {
            get
            {
                if (m_NodeTypeStr == null)
                    m_NodeTypeStr = emitterSpecified ? k_EmitterNodeTypeArgument : k_RepeaterNodeTypeArgument;
                return m_NodeTypeStr;
            }
        }
        
        internal static bool HeadlessEmitter
        {
            get
            {
                if (m_HeadlessEmitter == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_HeadlessEmitterArgument, out var startIndex))
                        return false;
                    
                    m_HeadlessEmitter = startIndex > -1;
                }

                return m_HeadlessEmitter.Value;
            }
        }
        
        internal static bool RepeaterReplacesHeadlessEmitter
        {
            get
            {
                if (m_RepeaterReplacesHeadlessEmitter == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_RepeaterReplacesHeadlessEmitterArgument, out var startIndex))
                        return false;
                    
                    m_RepeaterReplacesHeadlessEmitter = startIndex > -1;
                }

                return m_RepeaterReplacesHeadlessEmitter.Value;
            }
        }

        internal static bool ClusterLogicSpecified
        {
            get
            {
                if (m_ClusterLogicSpecified == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_EmitterNodeTypeArgument, out var startIndex) &&
                        !TryGetIndexOfNodeTypeArgument(k_RepeaterNodeTypeArgument, out startIndex))
                        return false;
                    
                    m_ClusterLogicSpecified = startIndex > -1;
                }

                return m_ClusterLogicSpecified.Value;
            }
        }

        private static int addressStartOffset => (emitterSpecified ? 3 : 2);
        
        private static bool TryCachePorts()
        {
            if (!TryGetIndexOfNodeTypeArgument(nodeTypeStr, out var startIndex))
                return false;
            
            ParsePorts(startIndex + addressStartOffset, out var rx, out var tx);
            m_RXPort = rx;
            m_TXPort = tx;
            
            return true;
        }

        internal static int rxPort
        {
            get
            {
                if (m_RXPort == null)
                {
                    if (!TryCachePorts())
                        return -1;
                }

                return m_RXPort.Value;
            }
        }
        
        internal static int txPort
        {
            get
            {
                if (m_RXPort == null)
                {
                    if (!TryCachePorts())
                        return -1;
                }

                return m_TXPort.Value;
            }
        }

        internal static string multicastAddress
        {
            get
            {
                if (m_MulticastAddress == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(nodeTypeStr, out var startIndex))
                        return null;
                    ParseMulticastAddressAndPort(startIndex + addressStartOffset, out m_MulticastAddress, out var rxPort, out var txPort);
                    
                    m_RXPort = rxPort;
                    m_TXPort = txPort;
                }

                return m_MulticastAddress;
            }
        }

        internal static byte nodeID
        {
            get
            {
                if (m_NodeID == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(nodeTypeStr, out var startIndex))
                        return 0;
                    
                    ParseId(startIndex + 1, out var id);
                    m_NodeID = id;
                }
                
                return m_NodeID.Value;
            }
        }
        
        internal static int repeaterCount
        {
            get
            {
                if (m_RepeaterCount == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(nodeTypeStr, out var startIndex))
                        return 0;
                    
                    ParseRepeaterCount(startIndex + 2, out var rc);
                    m_RepeaterCount = rc;
                }
                
                return m_RepeaterCount.Value;
            }
        }
        
        internal static bool debugFlag
        {
            get
            {
                if (m_DebugFlag == null)
                    m_DebugFlag = ParseDebug();

                return m_DebugFlag.Value;
            }
        }
        
        internal static bool TryGetIndexOfNodeTypeArgument (string nodeType, out int indexOfNodeTypeArgument)
        {
            indexOfNodeTypeArgument = Arguments.IndexOf(nodeType);
            return indexOfNodeTypeArgument > -1;
        }

        internal static bool emitterSpecified
        {
            get
            {
                if (m_EmitterSpecified == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_EmitterNodeTypeArgument, out var startIndex))
                        return false;
                    
                    m_EmitterSpecified = startIndex != -1;
                }

                return m_EmitterSpecified.Value;
            }
        }

        internal static bool repeaterSpecified
        {
            get
            {
                if (m_RepeaterSpecified == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_RepeaterNodeTypeArgument, out var startIndex))
                        return false;
                    
                    m_RepeaterSpecified = startIndex != -1;
                }

                return m_RepeaterSpecified.Value;
            }
        }

        internal static string adapterName
        {
            get
            {
                if (m_AdapterName == null)
                {
                    if (!TryGetIndexOfNodeTypeArgument(k_AdapterNameArgument, out var startIndex))
                        return null;
                    ParseAdapterName(out m_AdapterName);
                }

                return m_AdapterName;
            }
        }

        internal static int targetFPS
        {
            get
            {
                if (m_TargetFPS == null)
                {
                    TryParseTargetFPS(out var targetFPS);
                    m_TargetFPS = targetFPS;
                }

                return m_TargetFPS.Value;
            }
        }
        
        internal static Vector2Int ? gridSize
        {
            get
            {
                if (m_GridSize == null)
                {
                    if (!TryParseVector2Int(k_GridSize, out var value))
                        return null;
                    m_GridSize = value;
                }

                return m_GridSize.Value;
            }
        }
        
        
        internal static Vector2Int ? bezel
        {
            get
            {
                if (m_Bezel == null)
                {
                    if (!TryParseVector2Int(k_Bezel, out var value))
                        return null;
                    m_Bezel = value;
                }

                return m_Bezel.Value;
            }
        }
        
        internal static int ? overscan
        {
            get
            {
                if (m_Overscan == null)
                {
                    if (!TryParseIntArgument(k_Overscan, out var value))
                        return null;
                    m_Overscan = value;
                }

                return m_Overscan.Value;
            }
        }
        
        internal static Vector2Int ? physicalScreenSize
        {
            get
            {
                if (m_PhysicalScreenSize == null)
                {
                    if (!TryParseVector2Int(k_PhysicalScreenSize, out var value))
                        return null;
                    m_PhysicalScreenSize = value;
                }

                return m_PhysicalScreenSize.Value;
            }
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

        private static bool ParseDebug() =>
            Arguments.IndexOf(k_DebugArgument) > -1;

        private static void ParseAdapterName(out string adapterName) => TryParseStringArgument(k_AdapterNameArgument, out adapterName, optional: true);
        internal static bool TryParseHandshakeTimeout(out TimeSpan handshakeTimeout) => TryParseTimeSpanArgument(k_HandShakeTimeoutArgument, out handshakeTimeout, optional: true);
        internal static bool TryParseCommunicationTimeout(out TimeSpan communicationTimeout) => TryParseTimeSpanArgument(k_CommunicationTimeoutArgument, out communicationTimeout, optional: true);

        internal static bool TryParseTargetFPS(out int targetFPS)
        {
            if (!TryParseIntArgument(k_TargetFPS, out targetFPS, optional: true))
                return false;

            if (targetFPS < -1)
                targetFPS = -1;

            return true;
        }
        
        internal static bool TryParseVector2Int(string argumentName, out Vector2Int value)
        {
            value = Vector2Int.zero;
            if (!TryParseStringArgument(argumentName, out var str, optional: true))
                return false;

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

        internal static bool TryParseStringArgument (string argumentName, out string argumentValue, bool optional = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (!optional)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = null;
                return false;
            }

            argumentValue = Arguments[startIndex + 1];
            return argumentValue != null;
        }

        internal static bool TryParseTimeSpanArgument (string argumentName, out TimeSpan argumentValue, bool optional = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (!optional)
                    ClusterDebug.LogError($"There is no argument with name: \"{argumentName}\" specified.");

                argumentValue = TimeSpan.Zero;
                return false;
            }

            if (!int.TryParse(Arguments[startIndex + 1], out var milliseconds))
            {
                argumentValue = TimeSpan.Zero;
                return false;
            }

            if (milliseconds < 0)
            {
                argumentValue = TimeSpan.Zero;
                return false;
            }

            argumentValue = TimeSpan.FromMilliseconds(milliseconds);
            return argumentValue != null;
        }

        internal static bool TryParseIntArgument (string argumentName, out int argumentValue, bool optional = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (!optional)
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

        internal static bool TryParseAddressAndPort (string argumentName, out string address, out int port, bool optional = false)
        {
            var startIndex = Arguments.FindIndex(x => x == argumentName);
            if (startIndex < 0 || startIndex + 1 >= Arguments.Count)
            {
                if (!optional)
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