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
        bool IsILPostProcessRunner = false;

        string logName;

        public CodeGenDebug (string logName, bool isILPPRunner)
        {
            IsILPostProcessRunner = isILPPRunner;
            this.logName = logName;
        }

        const string ILPostProcessLogFolderPath = "./Temp/ClusterDisplay/Logs/";

        void ILPPWrite(string msg)
        {
            try
            {
                if (!Directory.Exists(ILPostProcessLogFolderPath))
                    Directory.CreateDirectory(ILPostProcessLogFolderPath);

                string logPath = $"{ILPostProcessLogFolderPath}{logName}.txt";

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
        }

        void ILPPLog(string msg) =>
            ILPPWrite($"ILPP {logName} Log: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Log: {msg}");

        void ILPPLogWarning(string msg) =>
            ILPPWrite($"ILPP {logName} Warning: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Warning: {msg}");

        void ILPPLogError(string msg) =>
            ILPPWrite($"ILPP {logName} Error: {msg}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Error: {msg}");

        void ILPPLogException(Exception exception, StackFrame stackFrame) =>
            ILPPWrite($"ILPP {logName} Exception: {exception.Message}\n{stackFrame.ToString()}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackFrame.ToString()}");
        
        void ILPPLogException(Exception exception, string stackTrace) =>
            ILPPWrite($"ILPP {logName} Exception: {exception.Message}\n{stackTrace}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}\n{stackTrace}");

        void ILPPLogException(Exception exception) =>
            ILPPWrite($"ILPP {logName} Exception: {exception.Message}");
            // Console.WriteLine($"ILPP {ilPostProcessorContextAssemblyName} Exception: {exception.Message}");
        #endif

        public void Log(string msg)
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

        public void LogWarning(string msg)
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

        public void LogError(string msg)
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

        public void LogException(Exception exception)
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
