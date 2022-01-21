using System;
using System.Threading;
using System.Threading.Tasks;

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
            
            // Console.WriteLine("Running Unity Cluster Display daemon");
            
            clusterListener.Run().GetAwaiter().GetResult();
        }
    }
}
