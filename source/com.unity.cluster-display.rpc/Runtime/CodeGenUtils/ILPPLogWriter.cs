using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.ClusterDisplay.RPC;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    internal class CodeGenDebug
    {
        #if UNITY_EDITOR
        static bool IsILPostProcessRunner = false;

        static string ilPostProcessorContextAssemblyName;
        public static void BeginILPostProcessing(string assemblyName)
        {
            IsILPostProcessRunner = true;
            ilPostProcessorContextAssemblyName = assemblyName;
        }

        const string ILPostProcessLogFolderPath = "./Temp/ClusterDisplay/Logs/";
        const string ILPostProcessLogFileName = "ClusterDisplay-ILPostProcessingLog.txt";
        
        static SharedMutex mutex = new SharedMutex("LogWriterMutex");

        static void ILPPWrite(string msg)
        {
            mutex.Lock();

            try
            {
                if (!Directory.Exists(ILPostProcessLogFolderPath))
                    Directory.CreateDirectory(ILPostProcessLogFolderPath);

                string logPath = $"{ILPostProcessLogFolderPath}{ILPostProcessLogFileName}";

                var fileStream = new FileStream(logPath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.ReadWrite);
                fileStream.Seek(0, SeekOrigin.End);

                var bytes = Encoding.UTF8.GetBytes(msg);
                fileStream.Write(bytes, 0, bytes.Length);

                fileStream.Write(Encoding.UTF8.GetBytes("\r\n"), 0, 2);

                fileStream.Close();
                fileStream.Dispose();
            }

            catch {}

            mutex.Release();
        }

        static void ILPPLog(string msg) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Log: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Log: {msg}");

        static void ILPPLogWarning(string msg) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Warning: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Warning: {msg}");

        static void ILPPLogError(string msg) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Error: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Error: {msg}");

        static void ILPPLogException(Exception exception, StackFrame stackFrame) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackFrame.ToString()}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackFrame.ToString()}");
        
        static void ILPPLogException(Exception exception, string stackTrace) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackTrace}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackTrace}");

        static void ILPPLogException(Exception exception) =>
            ILPPWrite($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}");
        #endif

        public static void Log(string msg)
        {
            #if UNITY_EDITOR
            if (IsILPostProcessRunner)
            {
                ILPPLog(msg);
                return;
            }
            #endif

            Debug.Log(msg);
        }

        public static void LogWarning(string msg)
        {
            #if UNITY_EDITOR
            if (IsILPostProcessRunner)
            {
                ILPPLogWarning(msg);
                return;
            }
            #endif

            Debug.LogWarning(msg);
        }

        public static void LogError(string msg)
        {
            #if UNITY_EDITOR
            if (IsILPostProcessRunner)
            {
                ILPPLogError(msg);
                return;
            }
            #endif

            Debug.LogError(msg);
        }

        public static void LogException(Exception exception)
        {
            #if UNITY_EDITOR
            if (IsILPostProcessRunner)
            {
                // var stackTrace = new StackTrace();
                // var stackTraceFrame = stackTrace.GetFrame(1);
                // ILPPLogException(exception, stackTraceFrame);
                // ILPPLogException(exception);
                ILPPLogException(exception, exception.StackTrace);
                return;
            }
            #endif

            Debug.LogException(exception);
        }
    }
}