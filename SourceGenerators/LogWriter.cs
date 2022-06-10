using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    public class Debug
    {
        const string k_LogFolderPath = "C:/ClusterDisplay/";
        const string k_LogFileName = "ClusterDisplay-SourceGenerator.txt";
        
        readonly static SharedMutex k_Mutex = new SharedMutex("SourceGeneratorLogMutex");

        static void Write(string msg)
        {
            k_Mutex.Lock();

            try
            {
                if (!Directory.Exists(k_LogFolderPath))
                {
                    Directory.CreateDirectory(k_LogFolderPath);
                }

                string logPath = $"{k_LogFolderPath}{k_LogFileName}";

                var fileStream = new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                fileStream.Seek(0, SeekOrigin.End);

                var bytes = Encoding.UTF8.GetBytes(msg);
                fileStream.Write(bytes, 0, bytes.Length);

                fileStream.Write(Encoding.UTF8.GetBytes("\r\n"), 0, 2);

                fileStream.Close();
                fileStream.Dispose();
            }

            catch (System.Exception exception)
            {
                throw exception;
            }

            k_Mutex.Release();
        }

        public static void Log(string msg) => Write($"Log: {msg}");
        public static void LogWarning(string msg) => Write($"Warning: {msg}");
        public static void LogError(string msg) => Write($"Error: {msg}");
        public static void LogException(Exception exception) => Write($"Exception: {exception.Message}\n{exception.StackTrace}");
    }
}
