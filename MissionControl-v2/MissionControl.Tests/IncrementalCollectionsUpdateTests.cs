using System.Diagnostics;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class IncrementalCollectionsUpdateTests
    {
        [TearDown]
        public void TearDown()
        {
            m_ProcessHelper.Dispose();

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
        public async Task UnknownCollection()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Ask only for a bad collection name
            Assert.ThrowsAsync<HttpRequestException>(() => m_ProcessHelper.GetIncrementalCollectionsUpdate(
                new List<(string, ulong)> { ("BadName", 0) }));

            // Ask for a good and a bad
            Assert.ThrowsAsync<HttpRequestException>(() => m_ProcessHelper.GetIncrementalCollectionsUpdate(
                new List<(string, ulong)> { (k_AssetsCollectionName, 0), ("BadName", 0) }));
        }

        [Test]
        public async Task MultipleCollections()
        {
            string tempFolder = GetTestTempFolder();
            await m_ProcessHelper.Start(tempFolder);

            // Ask without any change
            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_AssetsCollectionName, 0), (k_ComplexesCollectionName, 0) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Change something in launch complexes
            await m_ProcessHelper.PutLaunchComplex(k_Complex);

            // Should unblock the incremental collection update
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            Assert.That(blockingGetTask.Result.Count, Is.EqualTo(1));
            var collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_ComplexesCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            ulong complexesNextUpdate = collectionUpdate.NextUpdate;

            // Ask for the next update
            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_AssetsCollectionName, 0), (k_ComplexesCollectionName, complexesNextUpdate) });
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Change something to assets
            await using (var writeFile = File.OpenWrite(Path.Combine(tempFolder, "LaunchCatalog.json")))
            {
                writeFile.SetLength(0);
                await JsonSerializer.SerializeAsync(writeFile, new LaunchCatalog.Catalog(), Json.SerializerOptions);
            }
            AssetPost newAsset = new()
            {
                Name = "Empty asset",
                Description = "Asset with as less content as possible",
                Url = tempFolder
            };
            await m_ProcessHelper.PostAsset(newAsset);

            // Should unblock the incremental collection update
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            finishedTask = await Task.WhenAny(blockingGetTask, timeoutTask);
            Assert.That(finishedTask, Is.SameAs(blockingGetTask)); // Otherwise we timed out
            Assert.That(blockingGetTask.Result.Count, Is.EqualTo(1));
            collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_AssetsCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            ulong assetsNextUpdate = collectionUpdate.NextUpdate;

            // Now we want the update to contain both collections, however changing both collections quickly one after
            // the other does not guarantee we will receive both in a single update.  So instead ask from old versions
            // which guarantee we receive the answer with both immediately without waiting.
            blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_AssetsCollectionName, 0), (k_ComplexesCollectionName, 0) });
            await blockingGetTask;
            Assert.That(blockingGetTask.Result.Count, Is.EqualTo(2));

            collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_AssetsCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(assetsNextUpdate));

            collectionUpdate = Helpers.GetCollectionUpdate<LaunchComplex>(blockingGetTask.Result,
                k_ComplexesCollectionName);
            Assert.That(collectionUpdate.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(collectionUpdate.RemovedObjects, Is.Empty);
            Assert.That(collectionUpdate.NextUpdate, Is.EqualTo(complexesNextUpdate));
        }

        [Test]
        public async Task CancelGet()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            CancellationTokenSource cancellationTokenSource = new();

            // Try to get, should stay block since nothing changed so far...
            var blockingGetTask = m_ProcessHelper.GetIncrementalCollectionsUpdate(new List<(string, ulong)> {
                (k_AssetsCollectionName, 0), (k_ComplexesCollectionName, 0) }, cancellationTokenSource.Token);
            // ReSharper disable once MethodSupportsCancellation -> No need to cancel this Delay
            await Task.Delay(100); // So that it has the time to complete if blocking would fail
            Assert.That(blockingGetTask.IsCompleted, Is.False);

            // Cancel the request
            cancellationTokenSource.Cancel();

            // Check it was canceled
            Assert.ThrowsAsync<TaskCanceledException>(() => blockingGetTask);

            // Shutdown should be really quick (< 1 second).  A too long shutdown indicate MissionControl did not
            // correctly process the cancellation of the REST request.
            var stopwatch = Stopwatch.StartNew();
            m_ProcessHelper.Stop();
            stopwatch.Stop();
            Assert.That(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        }

        static readonly LaunchComplex k_Complex;

        static IncrementalCollectionsUpdateTests()
        {
            Guid complexAId = Guid.NewGuid();
            k_Complex = new(complexAId)
            {
                Name = "Complex A",
                HangarBay = new()
                {
                    Identifier = complexAId,
                    Endpoint = new("http://127.0.0.1:8100")
                },
                LaunchPads = new[] {
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "A1",
                        Endpoint = new("http://127.0.0.1:8201"),
                        SuitableFor = new []{ "clusterNode" }
                    },
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "A2",
                        Endpoint = new("http://127.0.0.1:8202"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "IncrementalCollectionsUpdateTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        const string k_AssetsCollectionName = "assets";
        const string k_ComplexesCollectionName = "complexes";

        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
