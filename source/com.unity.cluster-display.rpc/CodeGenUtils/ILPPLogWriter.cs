using System;
using System.IO;
using System.Text;
using Unity.ClusterDisplay.RPC;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    public class CodeGenDebug
    {
        #if UNITY_EDITOR
        private static bool IsILPostProcessRunner = false;

        public static void BeginILPostProcessing() =>
            IsILPostProcessRunner = true;

        private const string ILPostProcessLogFolderPath = "./Temp/ClusterDisplay/Logs/";
        private const string ILPostProcessLogFileName = "ClusterDisplay-ILPostProcessingLog.txt";
        
        private static SharedMutex mutex = new SharedMutex("LogWriterMutex");

        private static void ILPPWrite(string msg)
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

        private static void ILPPLog(string msg) =>
            ILPPWrite($"Log: {msg}");

        private static void ILPPLogWarning(string msg) =>
            ILPPWrite($"Warning: {msg}");

        private static void ILPPLogError(string msg) =>
            ILPPWrite($"Error: {msg}");

        private static void ILPPLogException(Exception exception) =>
            ILPPWrite($"Exception: {exception.Message}");
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
                ILPPLogException(exception);
                return;
            }
            #endif

            Debug.LogException(exception);
        }
    }
}