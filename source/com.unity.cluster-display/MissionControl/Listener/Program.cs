using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    static class Program
    {
        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, _) =>
            {
                cancellationTokenSource.Cancel();
                Environment.ExitCode = 0;
            };
            
            Console.WriteLine("Hello World!");
            // var launcher = new Launcher();
            
            var discovery = new ClusterListener(Constants.DiscoveryPort);
            discovery.Listen(cancellationTokenSource.Token).GetAwaiter().GetResult();
        }
    }
}
