using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClusterListenerService
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var logListener = new DefaultTraceListener();
            var consoleListener = new ConsoleTraceListener();
            logListener.LogFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "listener.log");

            Trace.Listeners.Add(logListener);
            Trace.Listeners.Add(consoleListener);
            
            CreateHostBuilder(args).Build().Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                })
                .UseWindowsService();
    }
}
