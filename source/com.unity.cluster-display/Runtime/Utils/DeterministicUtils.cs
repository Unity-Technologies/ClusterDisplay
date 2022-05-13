using System.Diagnostics;

namespace Unity.ClusterDisplay
{
    public static class DeterministicUtils
    {
        public static void LogCall ()
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod();

            ClusterDisplayState.TryGetFrame(out var frame);

            if (stackTrace.FrameCount > 2)
            {
                var executionStageMethod = stackTrace.GetFrame(2).GetMethod();
                ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in execution stage: \"{executionStageMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
                return;
            }

            ClusterDebug.Log($"Frame ({frame}): Called: \"{callingMethod.Name}\" in type: \"{callingMethod.DeclaringType.FullName}\".");
        }

        public static void LogCall (params object[] arguments)
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod();

            string argsStr = "";
            for (int i = 0; i < arguments.Length; i++)
                argsStr += $"\n\t{arguments[i]},";

            ClusterDisplayState.TryGetFrame(out var frame);
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
