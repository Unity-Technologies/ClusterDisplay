using System.Diagnostics;

namespace Unity.ClusterDisplay
{
    public static class DeterministicUtils
    {
        public static void LogCall ()
        {
            if (ClusterDisplayState.TryGetFrameId(out var frame))
            {
                StackTrace stackTrace = new StackTrace();
                var callingMethod = stackTrace.GetFrame(1).GetMethod();

                if (stackTrace.FrameCount > 2)
                {
                    var executionStageMethod = stackTrace.GetFrame(2).GetMethod();
                    ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in execution stage: \"{executionStageMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
                    return;
                }

                ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
            }
        }

        public static void LogCall (params object[] arguments)
        {
            if (ClusterDisplayState.TryGetFrameId(out var frame))
            {
                StackTrace stackTrace = new StackTrace();
                var callingMethod = stackTrace.GetFrame(1).GetMethod();

                string argsStr = "";
                for (int i = 0; i < arguments.Length; i++)
                    argsStr += $"\n\t{arguments[i]},";

                if (stackTrace.FrameCount > 2)
                {
                    var executionStageMethod = stackTrace.GetFrame(2).GetMethod();
                    ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in execution stage: \"{executionStageMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\" with arguments:{argsStr}");
                    return;
                }

                ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\" with arguments:{argsStr}");
            }
        }
    }
}
