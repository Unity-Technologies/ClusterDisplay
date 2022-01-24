using System;

namespace Unity.ClusterDisplay.MissionControl
{
    static class Program
    {
        static void Main(string[] args)
        {
            var clusterListener = new ClusterListener();
            Console.CancelKeyPress += (_, _) =>
            {
                clusterListener.Dispose();
                Environment.ExitCode = 0;
            };
            
            clusterListener.Run().GetAwaiter().GetResult();
        }
    }
}
