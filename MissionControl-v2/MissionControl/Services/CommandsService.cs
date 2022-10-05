using System;
using System.Diagnostics;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class CommandsServiceExtension
    {
        public static void AddCommandsService(this IServiceCollection services)
        {
            services.AddSingleton<CommandsService>();
        }
    }

    /// <summary>
    /// Service responsible for executing <see cref="Command"/>s.
    /// </summary>
    public class CommandsService
    {
        public CommandsService(ILogger<CommandsService> logger,
            IHostApplicationLifetime applicationLifetime,
            StatusService statusService)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;
            m_StatusService = statusService;
        }

        /// <summary>
        /// Execute the specified <see cref="Command"/>.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Unknown type of MissionCommand.</exception>
        public Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(Command command)
        {
            return command switch
            {
                RestartCommand    { } commandOfType => ExecuteAsync(commandOfType),
                ShutdownCommand   { } commandOfType => ExecuteAsync(commandOfType),
                ForceStateCommand { } commandOfType => ExecuteAsync(commandOfType),
                _ => throw new ArgumentException($"{command.GetType()} is not a supported mission command type.")
            };
        }

        Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(RestartCommand command)
        {
            var fullPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (fullPath == null)
            {
                throw new NullReferenceException("Failed getting current process path.");
            }
            string startupFolder = Path.GetDirectoryName(fullPath)!;
            string filename = Path.GetFileName(fullPath);

            ProcessStartInfo startInfo = new();
            startInfo.Arguments = GetCurrentProcessArguments();
            startInfo.FileName = filename;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;
            startInfo.WorkingDirectory = startupFolder;

            RemoteManagement.StartAndWaitForThisProcess(startInfo, command.TimeoutSec);

            m_ApplicationLifetime.StopApplication();
            return Task.FromResult((HttpStatusCode.Accepted, ""));
        }

        // ReSharper disable once UnusedParameter.Local
        Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(ShutdownCommand command)
        {
            m_ApplicationLifetime.StopApplication();
            return Task.FromResult((HttpStatusCode.Accepted, ""));
        }

        async Task<(HttpStatusCode code, string errorMessage)> ExecuteAsync(ForceStateCommand command)
        {
            if (command.KeepLocked)
            {
                // Do everything asynchronously
                TaskCompletionSource locked = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = Task.Run(async () =>
                {
                    using var lockedStatus = await m_StatusService.LockAsync();
                    locked.TrySetResult();

                    var savedState = lockedStatus.Value.State;
                    lockedStatus.Value.State = command.State;
                    lockedStatus.Value.ObjectChanged();

                    while (File.Exists(command.ControlFile))
                    {
                        await Task.Delay(25);
                    }

                    lockedStatus.Value.State = savedState;
                    lockedStatus.Value.ObjectChanged();
                });

                await locked.Task;
            }
            else
            {
                State savedState;
                using (var lockedStatus = await m_StatusService.LockAsync())
                {
                    savedState = lockedStatus.Value.State;
                    lockedStatus.Value.State = command.State;
                    lockedStatus.Value.ObjectChanged();
                }

                // Start a task monitoring the state to restore it when the specified file is gone...
                _ = Task.Run(async () =>
                {
                    while (File.Exists(command.ControlFile))
                    {
                        await Task.Delay(25);
                    }

                    using var lockedStatus = await m_StatusService.LockAsync();
                    lockedStatus.Value.State = savedState;
                    lockedStatus.Value.ObjectChanged();
                });
            }

            return (HttpStatusCode.OK, "");
        }

        /// <summary>
        /// Returns a string from the command line arguments used to launch this process.
        /// </summary>
        static string GetCurrentProcessArguments()
        {
            var arguments = RemoteManagement.FilterCommandLineArguments(Environment.GetCommandLineArgs()).Skip(1);
            return RemoteManagement.AssembleCommandLineArguments(arguments);
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;
        readonly StatusService m_StatusService;
    }
}
