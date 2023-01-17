using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    public static class MainClass
    {
        public static void Main()
        {
            MissionControl.Config missionControlConfig;
            try
            {
                var missionControlConfigJson = Environment.GetEnvironmentVariable(k_EnvMissionControlConfig);
                missionControlConfig = JsonConvert.DeserializeObject<MissionControl.Config>(missionControlConfigJson!,
                    Json.SerializerOptions);
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Environment variable {k_EnvMissionControlConfig} does " +
                    $"not contain the expected MissionControl configuration.");
            }
            Application application = new(new Uri(missionControlConfig!.LocalEntry));
            application.Start().Wait();
        }

        /// <summary>
        /// Setup a <see cref="CatalogBuilderDirective"/> to launch this capcom.
        /// </summary>
        /// <param name="searchPath">Path in which to search for the assembly containing this code.</param>
        /// <param name="toSetup">The <see cref="CatalogBuilderDirective"/> to setup.</param>
        /// <param name="filesFilter">Filter method used to accept or reject files found of the disk.  Called with the
        /// full path to the file and returns if the file is to be part of launchables or not.</param>
        public static void SetupCatalogBuilderDirective(string searchPath, CatalogBuilderDirective toSetup,
            Func<string, bool> filesFilter)
        {
            var capcomAssemblyPath = GetCapcomAssemblyPath(searchPath, filesFilter);
            HashSet<string> dependenciesSet = new();
            GetDependenciesOf(Path.GetDirectoryName(capcomAssemblyPath), Assembly.GetAssembly(typeof(MainClass)),
                dependenciesSet);

            capcomAssemblyPath = Path.GetRelativePath(searchPath, capcomAssemblyPath);
            var capcomAssemblyPathEncoded = capcomAssemblyPath.Replace('\\', '/');
            capcomAssemblyPathEncoded = Uri.EscapeDataString(capcomAssemblyPathEncoded);
            var dependencies = dependenciesSet.Select(d => Path.GetRelativePath(searchPath, d));

            toSetup.Launchable = new()
            {
                Name = "ClusterDisplay Capcom",
                Type = Launchable.CapcomType,
                LaunchPath = $"assemblyrun://{capcomAssemblyPathEncoded}/{typeof(MainClass).FullName}/{nameof(Main)}",
                LandingTime = TimeSpan.FromSeconds(2)
            };
            toSetup.ExclusiveFiles = new[] {capcomAssemblyPath};
            toSetup.Files = dependencies;
        }

        /// <summary>
        /// Search files in the given folder for the capcom assembly.
        /// </summary>
        /// <param name="searchPath">Path in which to search</param>
        /// <param name="filesFilter">Filter method used to accept or reject files found of the disk.  Called with the
        /// full path to the file and returns if the file is to be part of launchables or not.</param>
        static string GetCapcomAssemblyPath(string searchPath, Func<string, bool> filesFilter)
        {
            var scriptAssembly = Assembly.GetAssembly(typeof(MainClass));
            var scriptAssemblyPath = scriptAssembly.Location;
            var assemblyFilename = Path.GetFileName(scriptAssemblyPath);

            var files = Directory.GetFiles(searchPath, $"{assemblyFilename}", SearchOption.AllDirectories)
                .Where(filesFilter).ToList();
            return files.Count switch
            {
                0 => throw new FileNotFoundException($"Cannot find {assemblyFilename} in the build."),
                > 1 => throw new FileNotFoundException($"Found multiple {assemblyFilename} in the build, there " +
                    $"should be only one."),
                _ => files.First()
            };
        }

        /// <summary>
        /// Returns the dependencies of the given assembly in the given folder.
        /// </summary>
        /// <param name="searchPath">Path in which to search for assemblies.</param>
        /// <param name="assembly">The assembly to get the dependencies of.</param>
        /// <param name="dependencies">Set to which we add dependencies we discover.</param>
        static void GetDependenciesOf(string searchPath, Assembly assembly, HashSet<string> dependencies)
        {
            foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (referencedAssemblyName.Name != "netstandard")
                {
                    var referencedAssembly = GetAssemblyByName(referencedAssemblyName);

                    string referencedAssemblyFullPath = Path.Combine(searchPath, referencedAssembly.ManifestModule.Name);
                    if (!File.Exists(referencedAssemblyFullPath))
                    {
                        throw new FileNotFoundException($"Cannot find {referencedAssemblyFullPath}");
                    }

                    if (!dependencies.Add(referencedAssemblyFullPath))
                    {
                        continue;
                    }

                    GetDependenciesOf(searchPath, referencedAssembly, dependencies);
                }
            }
        }

        static Assembly GetAssemblyByName(AssemblyName name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().
                SingleOrDefault(assembly => assembly.GetName().Name == name.Name);
        }

        const string k_EnvMissionControlConfig = "MISSIONCONTROL_CONFIG";
    }
}
