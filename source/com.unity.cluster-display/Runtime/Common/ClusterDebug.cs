using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class ClusterDebug
    {
        [System.Diagnostics.Conditional("CLUSTER_DISPLAY_VERBOSE_LOGGING")]
        public static void Log (string msg) =>
            Debug.Log(msg);

        public static void LogWarning (string msg) =>
            Debug.LogWarning(msg);

        public static void LogError (string msg) =>
            Debug.LogError(msg);

        public static void LogException (System.Exception exception) =>
            Debug.LogException(exception);

        public static void Assert (bool assertion, string msg)
        {
            Debug.Assert(assertion, msg);
        }
    }
}
