using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace Unity.ClusterDisplay
{
    public static class DeterministicUtils
    {
        public static void LogCall ()
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod();

            if (stackTrace.FrameCount > 2)
            {
                var executionStageMethod = stackTrace.GetFrame(2).GetMethod();
                UnityEngine.Debug.Log($"Frame ({ClusterDisplayState.Frame}): Called: \"{callingMethod.Name}\" in execution stage: \"{executionStageMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
                return;
            }

            UnityEngine.Debug.Log($"Frame ({ClusterDisplayState.Frame}): Called: \"{callingMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
        }

        public static void LogCall (params object[] arguments)
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod();

            string argsStr = "";
            for (int i = 0; i < arguments.Length; i++)
                argsStr += $"\n\t{arguments[i]},";

            if (stackTrace.FrameCount > 2)
            {
                var executionStageMethod = stackTrace.GetFrame(2).GetMethod();
                UnityEngine.Debug.Log($"Frame ({ClusterDisplayState.Frame}): Called: \"{callingMethod.Name}\" in execution stage: \"{executionStageMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\" with arguments:{argsStr}");
                return;
            }

            UnityEngine.Debug.Log($"Frame ({ClusterDisplayState.Frame}): Called: \"{callingMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\" with arguments:{argsStr}");
        }
    }
}
