using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class CurrentMissionLaunchParametersForReviewControllerTests
    {
        public CurrentMissionLaunchParametersForReviewControllerTests()
        {
            m_ParametersForReview.SomethingChanged += _ => m_ParametersForReviewChanged.Signal();
        }

        [SetUp]
        public async Task SetUp()
        {
            await Task.WhenAll(m_HangarBayProcessHelper.Start(GetTestTempFolder()),
                m_ProcessHelper.Start(GetTestTempFolder()));

            lock (m_ParametersForReviewLock)
            {
                m_ParametersForReview.Clear();
                m_ParametersForReviewCancelSource = new();
                m_ParametersForReviewUpdater = TaskUpdateParametersForReview();
            }
        }

        [TearDown]
        public void TearDown()
        {
            lock (m_ParametersForReviewLock)
            {
                m_ParametersForReviewCancelSource.Cancel();
                try
                {
                    m_ParametersForReviewUpdater.Wait();
                }
                catch(Exception)
                {
                    // This is normal, just continue
                }
            }

            m_ProcessHelper.Dispose();
            foreach (var launchPadProcessHelper in m_LaunchPadsProcessHelper)
            {
                launchPadProcessHelper.Dispose();
            }
            m_LaunchPadsProcessHelper.Clear();
            m_HangarBayProcessHelper.Dispose();

            foreach (string folder in m_TestTempFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [Test]
        public async Task Launch()
        {
            // Launch
            var parametersValues = new [] {
                new LaunchParameterValue() { Id = "WithoutReview", Value = "Good value" },
                new LaunchParameterValue() { Id = "WithReview", Value = "Bad value" },
            };
            await PrepareMissionControl(1, k_LaunchCatalog, k_LaunchCatalogFilesContent, parametersValues);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            // Wait for the parameters to review to be updated
            await WaitForParametersToReview(c => c.Count > 0);
            LaunchParameterForReview toBeReviewed;
            lock (m_ParametersForReviewLock)
            {
                Assert.That(m_ParametersForReview.Count, Is.EqualTo(1));
                toBeReviewed = m_ParametersForReview.Values.First().DeepClone();
            }
            Assert.That(toBeReviewed.LaunchPadId, Is.EqualTo(m_LaunchPadsProcessHelper.First().Id));
            Assert.That(toBeReviewed.Value.Id, Is.EqualTo("WithReview"));
            Assert.That(toBeReviewed.Value.Value, Is.EqualTo("Bad value"));
            Assert.That(toBeReviewed.Ready, Is.False);

            // While at it, test the "manual gets" (not the incremental collection update) part of the controller.
            var toBeReviewedArray = await m_ProcessHelper.GetLaunchParametersForReview();
            Assert.That(toBeReviewedArray, Is.EqualTo(new[] { toBeReviewed }));
            var toBeReviewedSingle = await m_ProcessHelper.GetLaunchParameterForReview(toBeReviewed.Id);
            Assert.That(toBeReviewedSingle, Is.EqualTo(toBeReviewed));
            Assert.That(await m_ProcessHelper.GetLaunchParameterForReviewStatusCode(Guid.NewGuid()),
                Is.EqualTo(HttpStatusCode.NotFound));

            // Try to send buggy reviews
            LaunchParameterForReview badReview = new(Guid.NewGuid());
            Assert.That(await m_ProcessHelper.PutReviewedLaunchParameterStatusCode(badReview),
                Is.EqualTo(HttpStatusCode.NotFound));
            badReview = toBeReviewed.DeepClone();
            badReview.LaunchPadId = Guid.NewGuid();
            Assert.That(await m_ProcessHelper.PutReviewedLaunchParameterStatusCode(badReview),
                Is.EqualTo(HttpStatusCode.BadRequest));

            // At last, lets do a real review!
            toBeReviewed.Value.Value = "Reviewed value";
            toBeReviewed.Ready = true;
            await m_ProcessHelper.PutReviewedLaunchParameter(toBeReviewed);

            // Wait for the process to be launched
            var launchParameters = await GetLaunchedProcessParameters(m_LaunchPadsProcessHelper.First());
            Assert.That(launchParameters["WithoutReview"]!.ToString(), Is.EqualTo("Good value"));
            Assert.That(launchParameters["WithReview"]!.ToString(), Is.EqualTo("Reviewed value"));

            // The to review collection should be cleaned after the process is started
            await WaitForParametersToReview(c => c.Count == 0);
            lock (m_ParametersForReviewLock)
            {
                Assert.That(m_ParametersForReview, Is.Empty);
            }
        }

        async Task WaitForParametersToReview(Func<IncrementalCollection<LaunchParameterForReview>, bool> predicate)
        {
            var waitLimit = Stopwatch.StartNew();
            while (waitLimit.Elapsed < TimeSpan.FromSeconds(5))
            {
                Task? toWaitOn = null;
                lock (m_ParametersForReviewLock)
                {
                    if (predicate(m_ParametersForReview))
                    {
                        break;
                    }
                }
                if (toWaitOn != null)
                {
                    await toWaitOn.WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
        }

        static async Task<JsonNode> GetLaunchedProcessParameters(LaunchPadProcessHelper launchpadProcessHelper)
        {
            var pidTxtPath = Path.Combine(launchpadProcessHelper.LaunchFolder, "parameters.json");

            var elapsedTime = Stopwatch.StartNew();
            while (elapsedTime.Elapsed < TimeSpan.FromSeconds(15))
            {
                try
                {
                    var jsonString = await File.ReadAllTextAsync(pidTxtPath);
                    var ret = JsonNode.Parse(jsonString);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
                catch
                {
                    // Just ignore, this is normal, process is not launched yet.  Take a small break and try again
                    await Task.Delay(25);
                }
            }
            Assert.Fail("Cannot find parameters.json (did the launch failed?)");
            return new JsonObject();
        }

        async Task PrepareMissionControl(int nbrLaunchPads, LaunchCatalog.Catalog catalog,
            Dictionary<string, MemoryStream> filesContent, IEnumerable<LaunchParameterValue> parametersValue)
        {
            List<Task> launchPads = new();
            for (int launchPadIdx = 0; launchPadIdx < nbrLaunchPads; ++launchPadIdx)
            {
                var processHelper = new LaunchPadProcessHelper();
                launchPads.Add(processHelper.Start(GetTestTempFolder(), 8200 + launchPadIdx));
                m_LaunchPadsProcessHelper.Add(processHelper);
            }
            await Task.WhenAll(launchPads);

            string assetUrl = await CreateAsset(GetTestTempFolder(), catalog, new(), filesContent);
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);

            LaunchComplex launchComplex = new(Guid.NewGuid());
            launchComplex.LaunchPads = m_LaunchPadsProcessHelper.Select(lpph => new LaunchPad() {
                Identifier = lpph.Id,
                Endpoint = lpph.EndPoint,
                SuitableFor = new[] { "clusterNode" }
            }).ToList();
            await m_ProcessHelper.PutLaunchComplex(launchComplex);

            LaunchConfiguration launchConfiguration = new() {
                AssetId = assetId,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = launchComplex.Id,
                        LaunchPads = m_LaunchPadsProcessHelper.Select(
                            lpph => new LaunchPadConfiguration() { Identifier = lpph.Id, LaunchableName = "Cluster Node" }).ToList()
                    }
                },
                Parameters = parametersValue
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);
        }

        async Task TaskUpdateParametersForReview()
        {
            List<(string name, ulong fromVersion)> incrementalUpdatesToGet = new(){
                (k_ForReviewCollectionName, 0) };
            while (!m_ParametersForReviewCancelSource.IsCancellationRequested)
            {
                var updates = await m_ProcessHelper.GetIncrementalCollectionsUpdate(incrementalUpdatesToGet,
                    m_ParametersForReviewCancelSource.Token);
                var update = GetCollectionUpdate<LaunchParameterForReview>(updates, k_ForReviewCollectionName);
                incrementalUpdatesToGet[0] = (k_ForReviewCollectionName, update.NextUpdate);
                lock (m_ParametersForReviewLock)
                {
                    m_ParametersForReview.ApplyDelta(update);
                }
            }
        }

        static readonly LaunchCatalog.Catalog k_LaunchCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    LaunchPath = "launch.ps1",
                    GlobalParameters = new[] {
                        new LaunchCatalog.LaunchParameter() { Id = "WithoutReview",
                            Type = LaunchCatalog.LaunchParameterType.String, DefaultValue = "Default1" },
                        new LaunchCatalog.LaunchParameter() { Id = "WithReview",
                            Type = LaunchCatalog.LaunchParameterType.String, ToBeRevisedByCapcom = true,
                            DefaultValue = "Default2" }
                    }
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_LaunchCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$env:LAUNCH_DATA | Out-File \"parameters.json\" \n" +
                "while ( $true )                                 \n" +
                "{                                               \n" +
                "    Start-Sleep -Seconds 60                     \n" +
                "}") }
        };

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "CurrentMissionLaunchParametersForReviewControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_ForReviewCollectionName = "currentMission/launchParametersForReview";

        HangarBayProcessHelper m_HangarBayProcessHelper = new();
        List<LaunchPadProcessHelper> m_LaunchPadsProcessHelper = new();
        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();

        object m_ParametersForReviewLock = new();
        IncrementalCollection<LaunchParameterForReview> m_ParametersForReview = new();
        CancellationTokenSource m_ParametersForReviewCancelSource = new();
        Task m_ParametersForReviewUpdater = Task.CompletedTask;
        AsyncConditionVariable m_ParametersForReviewChanged = new();
    }
}
