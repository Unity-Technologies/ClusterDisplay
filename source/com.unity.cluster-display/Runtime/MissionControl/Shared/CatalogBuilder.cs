using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Information about a <see cref="Launchable"/> to add to the generated <see cref="Catalog"/>.
    /// </summary>
    public class CatalogBuilderDirective
    {
        /// <summary>
        /// The <see cref="Launchable"/> to add to the <see cref="Catalog"/>.
        /// </summary>
        /// <remarks>Payloads of the <see cref="Launchable"/> property will be ignored.</remarks>
        public Launchable Launchable { get; set; }

        /// <summary>
        /// List of file paths (relative to the folder from which the catalog is built) that are to be included
        /// exclusively in this launchable.
        /// </summary>
        /// <remarks>Can include wildcards (* and ?.).<br/><br/>Two (or more) launchables can have the same files in its
        /// exclusive files list, in that case only those ones will have that file.</remarks>
        public IEnumerable<string> ExclusiveFiles { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// List of file paths (relative to the folder from which the catalog is built) that are to be included in this
        /// launchable (and potentially in other launchables).
        /// </summary>
        /// <remarks>Can include wildcards (* and ?.)</remarks>
        public IEnumerable<string> Files { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// List of files paths (relative to the folder from which the catalog is built) that are not to be included in
        /// this launchable (even if part of <see cref="Files"/> or <see cref="ExcludedFiles"/>).
        /// </summary>
        /// <remarks>Can include wildcards (* and ?.)</remarks>
        public IEnumerable<string> ExcludedFiles { get; set; } = Enumerable.Empty<string>();
    }

    /// <summary>
    /// Class to compute the content of a LaunchCatalog.json for a cluster display build.
    /// </summary>
    public static class CatalogBuilder
    {
        /// <summary>
        /// Build a <see cref="Catalog"/>.
        /// </summary>
        /// <param name="path">Path that contains the files include in the <see cref="Launchable"/>s of the
        /// <see cref="Catalog"/>.</param>
        /// <param name="directives">List of <see cref="CatalogBuilderDirective"/> guiding the creation of the
        /// <see cref="Catalog"/>.</param>
        /// <exception cref="ArgumentException">Something not valid in <paramref name="directives"/>.</exception>
        public static Catalog Build(string path, IEnumerable<CatalogBuilderDirective> directives)
        {
            // First step, let's be sure every launchable have a name and it is unique
            if (directives.Any(cbd => cbd.Launchable == null))
            {
                throw new ArgumentException("Every directive need to have a Launchable.", nameof(directives));
            }
            if (directives.Select(cbd => cbd.Launchable.Name).Distinct().Count() < directives.Count())
            {
                throw new ArgumentException("Some launchable share the same name, every launchable must have a unique " +
                    "name within the LaunchCatalog.", nameof(directives));
            }
            if (directives.Any(cbd => string.IsNullOrEmpty(cbd.Launchable.Name)))
            {
                throw new ArgumentException("Launchable name cannot be empty.", nameof(directives));
            }

            // Validate we have all mandatory parts of launchanbles
            if (directives.Any(cbd => string.IsNullOrEmpty(cbd.Launchable.Type)))
            {
                throw new ArgumentException("Launchable type cannot be empty.", nameof(directives));
            }
            if (directives.Any(cbd => string.IsNullOrEmpty(cbd.Launchable.LaunchPath)))
            {
                throw new ArgumentException("Launchable launchPath cannot be empty.", nameof(directives));
            }

            // Now we can build the catalog
            var launchablesInformation = GetLaunchablesFiles(path, directives);
            var payloads = ComputePayloads(path, launchablesInformation);

            Catalog ret = new();
            ret.Payloads = payloads.ToList();
            foreach (var launchableInformation in launchablesInformation)
            {
                launchableInformation.Launchable.Payloads = launchableInformation.Payloads.Select(p => p.Name)
                    .OrderBy(n => n).ToList();
            }
            ret.Launchables = launchablesInformation.Select(li => li.Launchable).ToList();
            return ret;
        }

        /// <summary>
        /// Information about a <see cref="Launchable"/> accumulated while computing entries of the produced
        /// <see cref="Catalog"/>.
        /// </summary>
        class LaunchableInformation
        {
            /// <summary>
            /// The <see cref="CatalogBuilderDirective"/> for which we are accumulating information.
            /// </summary>
            public CatalogBuilderDirective From { get; set; }

            /// <summary>
            /// The <see cref="Launchable"/> to add to the <see cref="Catalog"/>.
            /// </summary>
            /// <remarks>Payloads of the <see cref="Launchable"/> property will be ignored.</remarks>
            public Launchable Launchable { get; set; }

            /// <summary>
            /// List of files in this launchable
            /// </summary>
            public HashSet<string> Files { get; set; } = new();

            /// <summary>
            /// Payloads used by this launchable.
            /// </summary>
            public List<Payload> Payloads { get; } = new();
        }

        /// <summary>
        /// Used to compare equality <see cref="List{LaunchableInformation}"/>.
        /// </summary>
        class LaunchableInformationListEqualityComparer : IEqualityComparer<List<LaunchableInformation>>
        {
            public bool Equals(List<LaunchableInformation> x, List<LaunchableInformation> y)
            {
                if ((x == null) != (y == null))
                {
                    return false;
                }

                if (x == null)
                {
                    Debug.Assert(y == null);
                    return true;
                }

                return x.SequenceEqual(y);
            }

            public int GetHashCode(List<LaunchableInformation> obj)
            {
                int currentHash = obj.GetHashCode();
                foreach (var launchableInformation in obj)
                {
                    currentHash = HashCode.Combine(launchableInformation);
                }

                return currentHash;
            }

            public static LaunchableInformationListEqualityComparer Instance = new();
        }

        /// <summary>
        /// Used to order <see cref="List{LaunchableInformation}"/>.
        /// </summary>
        class LaunchableInformationListComparer : IComparer<List<LaunchableInformation>>
        {
            public int Compare(List<LaunchableInformation> x, List<LaunchableInformation> y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(null, y)) return 1;
                if (ReferenceEquals(null, x)) return -1;
                if (x.Count != y.Count) return x.Count - y.Count;
                int count = x.Count;
                for (int index = 0; index < count; ++index)
                {
                    int stringCompareResult = StringComparer.InvariantCulture.Compare(
                        x[index].Launchable.Name, y[index].Launchable.Name);
                    if (stringCompareResult != 0)
                    {
                        return stringCompareResult;
                    }
                }

                return 0;
            }

            public static LaunchableInformationListComparer Instance = new();
        }

        /// <summary>
        /// Returns a set of relative file names based on the given file list.
        /// </summary>
        /// <param name="path">Base search path.</param>
        /// <param name="list">The list of files to return.</param>
        /// <param name="excludedList">Files to be removed from the returned set even if present in
        /// <paramref name="list"/>.</param>
        static HashSet<string> GetFiles(string path, IEnumerable<string> list, IEnumerable<string> excludedList)
        {
            var excludedFiles = excludedList.Any() ? GetFiles(path, excludedList, Enumerable.Empty<string>()) : new();

            HashSet<string> ret = new();
            foreach (var inList in list)
            {
                var files = Directory.GetFiles(path, inList, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relative = Path.GetRelativePath(path, file);
                    if (!excludedFiles.Contains(relative))
                    {
                        ret.Add(relative);
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Get the files of every Launchables.
        /// </summary>
        /// <param name="path">Path that contains the files include in the <see cref="Launchable"/>s of the
        /// <see cref="Catalog"/>.</param>
        /// <param name="directives">List of <see cref="CatalogBuilderDirective"/> guiding the creation of the
        /// <see cref="Catalog"/>.</param>
        static IEnumerable<LaunchableInformation> GetLaunchablesFiles(string path, IEnumerable<CatalogBuilderDirective> directives)
        {
            List<LaunchableInformation> ret = new();

            // First deal with exclusive files
            HashSet<string> allExcludedFiles = new();
            foreach (var directive in directives)
            {
                LaunchableInformation information = new()
                {
                    From = directive,
                    // Lazy cloning -> No need to be fast anyway, so let's use something that we know will always work.
                    Launchable = JsonConvert.DeserializeObject<Launchable>(
                        JsonConvert.SerializeObject(directive.Launchable, Json.SerializerOptions), Json.SerializerOptions),
                    Files = GetFiles(path, directive.ExclusiveFiles, directive.ExcludedFiles)
                };
                ret.Add(information);
                allExcludedFiles.UnionWith(information.Files);
            }

            // Then find the other files
            foreach (var launchableInformation in ret)
            {
                HashSet<string> excludedFiles = new(allExcludedFiles);
                excludedFiles.UnionWith(launchableInformation.From.ExcludedFiles);
                launchableInformation.Files.UnionWith(GetFiles(path, launchableInformation.From.Files, excludedFiles));
            }

            // Done
            return ret.Where(li => li.Files.Any());
        }

        /// <summary>
        /// Compute the PayloadFile for a file (longest part is computing the MD5 checksum).
        /// </summary>
        /// <param name="basePath">Base path for <paramref name="fileRelativePath"/>.</param>
        /// <param name="fileRelativePath">Relative path to the file (relative to <paramref name="basePath"/>).</param>
        static async Task<PayloadFile> GetPayloadFile(string basePath, string fileRelativePath)
        {
            string fullName = Path.Combine(basePath, fileRelativePath);
            await using var fileStream = File.OpenRead(fullName);
            using var md5Calculator = MD5.Create();

            // The following would have been nice, but it looks like it only exists since .Net 5...
            //var hash = md5Calculator.ComputeHash(fileStream);

            // So let's do it manually
            const int readChunkSize = 1024 * 1024;
            byte[] readBuffer = new byte[readChunkSize];
            for (int readLength; (readLength = await fileStream.ReadAsync(readBuffer, 0, readChunkSize).ConfigureAwait(false)) > 0;)
            {
                int ret = md5Calculator.TransformBlock(readBuffer, 0, readLength, null, 0);
                Debug.Assert(ret == readLength);
            }
            md5Calculator.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = md5Calculator.Hash;
            Debug.Assert(hash != null);

            // Done
            return new() {Path = fileRelativePath, Md5 = ConvertHelpers.ToHexString(hash)};
        }

        /// <summary>
        /// Compute the payloads necessary for every launchable to contain the right files.
        /// </summary>
        /// <param name="path">Path that contains the files include in the <see cref="Launchable"/>s of the
        /// <see cref="Catalog"/>.</param>
        /// <param name="launchablesInformation">Information about the <see cref="Launchable"/>s that are to be created
        /// to fill the <see cref="Catalog"/> we have to create.</param>
        static IEnumerable<Payload> ComputePayloads(string path, IEnumerable<LaunchableInformation> launchablesInformation)
        {
            // For each file lets find the list of launchables they are a part of
            Dictionary<string, List<LaunchableInformation>> filenameToLaunchablesInformation = new();
            foreach (var launchableInformation in launchablesInformation)
            {
                foreach (var file in launchableInformation.Files)
                {
                    if (!filenameToLaunchablesInformation.TryGetValue(file, out var launchablesInformationOfFile))
                    {
                        launchablesInformationOfFile = new();
                        filenameToLaunchablesInformation.Add(file, launchablesInformationOfFile);
                    }
                    launchablesInformationOfFile.Add(launchableInformation);
                }
            }

            // Compute MD5 checksum for every file (this is the part that can be long, so we run it in parallel).
            var payloadFileTaskToLaunchablesInformation = filenameToLaunchablesInformation
                .Select(p => new KeyValuePair<Task<PayloadFile>, List<LaunchableInformation>>(GetPayloadFile(path, p.Key), p.Value));
            Task.WhenAll(payloadFileTaskToLaunchablesInformation.Select(p => p.Key)).Wait();
            var payloadFileToLaunchablesInformation = payloadFileTaskToLaunchablesInformation
                .Select(p => new KeyValuePair<PayloadFile, List<LaunchableInformation>>(p.Key.Result, p.Value))
                .OrderBy(p => p.Key.Path);

            // Lets now create the payloads.  Every files used by the same launchables will be in the same payload.
            Dictionary<List<LaunchableInformation>, Payload> launchablesListToPayload =
                new(LaunchableInformationListEqualityComparer.Instance);
            foreach (var pair in payloadFileToLaunchablesInformation)
            {
                if (!launchablesListToPayload.TryGetValue(pair.Value, out var payload))
                {
                    payload = new();
                    launchablesListToPayload.Add(pair.Value, payload);

                    // Assign a name if payload is used by only one launchable
                    if (pair.Value.Count == 1)
                    {
                        payload.Name = pair.Value.First().Launchable.Name;
                    }
                    // else we will do it once we are done creating the launchables

                    // Dispatch the payload into every launchable it contains files for
                    foreach (var launchableInformation in pair.Value)
                    {
                        launchableInformation.Payloads.Add(payload);
                    }
                }
                payload.Files.Add(pair.Key);
            }

            // Prepare the returned list of payloads
            // Remark: We order them based on launchables name so that they are always in the same order when running an
            // identical build...
            var ret = launchablesListToPayload
                .OrderBy(p => p.Key, LaunchableInformationListComparer.Instance)
                .Select(p => p.Value);

            // Last step, assign names to the payloads
            AssignNameToPayloads(ret);

            // Done
            return ret;
        }

        /// <summary>
        /// Assign a name to the given <see cref="Payload"/>s (if they don't already have one).
        /// </summary>
        /// <param name="payloads">The <see cref="Payload"/>s.</param>
        static void AssignNameToPayloads(IEnumerable<Payload> payloads)
        {
            HashSet<string> payloadNames = new();
            int withoutNameCount = 0;
            foreach (var payload in payloads)
            {
                if (string.IsNullOrEmpty(payload.Name))
                {
                    ++withoutNameCount;
                }
                else
                {
                    payloadNames.Add(payload.Name);
                }
            }

            int sharedCounter = 0;
            foreach (var payload in payloads)
            {
                if (string.IsNullOrEmpty(payload.Name))
                {
                    if (withoutNameCount == 1 && !payloadNames.Contains(k_SharedPayloadName))
                    {
                        payload.Name = k_SharedPayloadName;
                        break;
                    }

                    while (string.IsNullOrEmpty(payload.Name))
                    {
                        string candidateName = k_SharedPayloadName + (sharedCounter++).ToString();
                        if (payloadNames.Add(candidateName))
                        {
                            payload.Name = candidateName;
                        }
                    }
                }
            }
        }

        const string k_SharedPayloadName = "shared";
    }
}
