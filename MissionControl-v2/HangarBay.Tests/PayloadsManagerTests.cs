using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class PayloadsManagerTests
    {
        [SetUp]
        public void SetUp()
        {

        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(m_LoggerStub.Messages, Is.Empty);

            foreach (string folder in m_StorageFolders)
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
        public async Task FillSaveAndLoad()
        {
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);

            Guid repeatedFileId = Guid.NewGuid();
            Guid uniqueFile1 = Guid.NewGuid();
            Guid uniqueFile2 = Guid.NewGuid();

            var payload1Id = Guid.NewGuid();
            var payload1 = new Payload();
            payload1.Files = new[] {
                new PayloadFile() {Path="someFile",        FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="someOtherFile",   FileBlob = uniqueFile1,    CompressedSize = 28, Size = 56},
                new PayloadFile() {Path="repeat/someFile", FileBlob = repeatedFileId, CompressedSize = 42, Size = 84}
            };

            var payload2Id = Guid.NewGuid();
            var payload2 = new Payload();
            payload2.Files = new[] {
                new PayloadFile() {Path="sameAsSomeFile",  FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="somethingElse",   FileBlob = uniqueFile2,    CompressedSize = 28, Size = 56}
            };

            var someCookie = new object();
            int fetchCallCount = 0;
            payloadsManager.FetchFileCallback = (payloadId, cookie) =>
            {
                Interlocked.Increment(ref fetchCallCount);
                if (cookie != someCookie)
                {
                    throw new ArgumentException($"Bad cookie!");
                }
                if (payloadId == payload1Id) return Task.FromResult(payload1);
                if (payloadId == payload2Id) return Task.FromResult(payload2);
                throw new ArgumentException($"Unknown {nameof(payloadId)}");
            };

            var resultingPayload1 = await payloadsManager.GetPayload(payload1Id, someCookie);
            Assert.That(resultingPayload1, Is.SameAs(payload1));
            Assert.That(fetchCallCount, Is.EqualTo(1));
            Assert.That(m_FileBlobCacheStub.CompareEntries(new[] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile1,    CompressedSize = 28, Size = 56 },
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 }
            }), Is.True);
            m_FileBlobCacheStub.Clear();

            var resultingPayload2 = await payloadsManager.GetPayload(payload2Id, someCookie);
            Assert.That(resultingPayload2, Is.SameAs(payload2));
            Assert.That(fetchCallCount, Is.EqualTo(2));
            Assert.That(m_FileBlobCacheStub.CompareEntries(new[] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile2,    CompressedSize = 28, Size = 56 }
            }), Is.True);
            m_FileBlobCacheStub.Clear();

            // Reload
            var payloadsManagerNew = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);
            payloadsManagerNew.FetchFileCallback = payloadsManager.FetchFileCallback;
            // We cannot compare entries in details since reload order is "random"
            Assert.That(m_FileBlobCacheStub.Entries.Count, Is.EqualTo(5));
            m_FileBlobCacheStub.Clear();

            resultingPayload1 = await payloadsManagerNew.GetPayload(payload1Id, someCookie);
            Assert.That(resultingPayload1, Is.Not.SameAs(payload1)); // Since it got reloaded from json
            Assert.That(ComparePayload(resultingPayload1, payload1), Is.True);
            resultingPayload2 = await payloadsManagerNew.GetPayload(payload2Id, someCookie);
            Assert.That(resultingPayload1, Is.Not.SameAs(payload2)); // Since it got reloaded from json
            Assert.That(ComparePayload(resultingPayload2, payload2), Is.True);
            Assert.That(fetchCallCount, Is.EqualTo(2)); // Should not fetch again, everything was reloaded
        }

        [Test]
        public async Task LoadErrors()
        {
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);

            Guid repeatedFileId = Guid.NewGuid();
            Guid uniqueFile1 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            var payload = new Payload();
            payload.Files = new[] {
                new PayloadFile() {Path="someFile",        FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="someOtherFile",   FileBlob = uniqueFile1,    CompressedSize = 28, Size = 56},
                new PayloadFile() {Path="repeat/someFile", FileBlob = repeatedFileId, CompressedSize = 42, Size = 84}
            };

            int fetchCallCount = 0;
            payloadsManager.FetchFileCallback = (id, _) =>
            {
                Interlocked.Increment(ref fetchCallCount);
                if (id == payloadId) return Task.FromResult(payload);
                throw new ArgumentException($"Unknown {nameof(payloadId)}");
            };

            var resultingPayload1 = await payloadsManager.GetPayload(payloadId);
            Assert.That(resultingPayload1, Is.SameAs(payload));
            Assert.That(fetchCallCount, Is.EqualTo(1));
            Assert.That(m_FileBlobCacheStub.Entries.Count, Is.EqualTo(3));
            m_FileBlobCacheStub.Entries.Clear();

            // Add an unrelated json file in the folder and try to load
            string unrelatedFilePath = Path.Combine(storageFolder, "Unrelated.json");
            await File.WriteAllTextAsync(unrelatedFilePath, "{}");

            Assert.That(m_LoggerStub.Messages, Is.Empty);
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);
            _ = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("Unexpected filename"), Is.True);
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("Unrelated.json"), Is.True);
            m_LoggerStub.Messages.Clear();
            Assert.That(m_FileBlobCacheStub.Entries.Count, Is.EqualTo(3)); // The bad file was skipped but we did continue loading
            m_FileBlobCacheStub.Entries.Clear();
            File.Delete(unrelatedFilePath);

            // Add a .json file with non json content in it and try to load
            string badPayloadFilename = $"{Guid.NewGuid()}.json";
            string badPayloadPath = Path.Combine(storageFolder, badPayloadFilename);
            await File.WriteAllTextAsync(badPayloadPath, "This is not a json");

            Assert.That(m_LoggerStub.Messages, Is.Empty);
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);
            _ = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("Failed loading back"), Is.True);
            Assert.That(m_LoggerStub.Messages[0].Content.Contains(badPayloadFilename), Is.True);
            m_LoggerStub.Messages.Clear();
            Assert.That(m_FileBlobCacheStub.Entries.Count, Is.EqualTo(3)); // The bad file was skipped but we did continue loading
            m_FileBlobCacheStub.Entries.Clear();
            File.Delete(badPayloadPath);

            // Fake a loading problem (that would be cause by conflicting sizes for example) when loading back assets.
            m_FileBlobCacheStub.FakeIncreaseUsageCountErrorIn = 3;

            Assert.That(m_LoggerStub.Messages, Is.Empty);
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);
            _ = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("processing files of"), Is.True);
            Assert.That(m_LoggerStub.Messages[0].Content.Contains($"{payloadId}.json"), Is.True);
            m_LoggerStub.Messages.Clear();
            Assert.That(m_FileBlobCacheStub.CompareEntries(new FileBlobCacheStub.Entry[] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile1,    CompressedSize = 28, Size = 56 },
                new FileBlobCacheStub.DecreaseEntry() { Id = uniqueFile1 },
                new FileBlobCacheStub.DecreaseEntry() { Id = repeatedFileId }
            }), Is.True);
            m_FileBlobCacheStub.Entries.Clear();
            File.Delete(badPayloadPath);
        }

        class TestException: Exception
        {
        }

        [Test]
        public async Task FetchFailures()
        {
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);

            var somePayloadId = Guid.NewGuid();

            // Test an exception during FetchFileCallback
            payloadsManager.FetchFileCallback = (_, _) => throw new TestException();
            Assert.That(() => payloadsManager.GetPayload(somePayloadId), Throws.TypeOf<TestException>());

            // Test that everything get rolled-back when a problem is detected while increasing entries usage
            Guid repeatedFileId = Guid.NewGuid();
            Guid uniqueFile1 = Guid.NewGuid();
            Guid uniqueFile2 = Guid.NewGuid();
            var payload = new Payload();
            payload.Files = new[] {
                new PayloadFile() {Path="someFile",        FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="someOtherFile",   FileBlob = uniqueFile1,    CompressedSize = 28, Size = 56},
                new PayloadFile() {Path="repeat/someFile", FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="somethingElse",   FileBlob = uniqueFile2,    CompressedSize = 280, Size = 560}
            };
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);
            payloadsManager.FetchFileCallback = (_, _) => Task.FromResult(payload);

            m_FileBlobCacheStub.FakeIncreaseUsageCountErrorIn = 4;
            Assert.That(() => payloadsManager.GetPayload(somePayloadId), Throws.TypeOf<FileBlobCacheStub.FakeException>());
            Assert.That(m_FileBlobCacheStub.CompareEntries(new FileBlobCacheStub.Entry[] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile1,    CompressedSize = 28, Size = 56 },
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.DecreaseEntry() { Id = repeatedFileId },
                new FileBlobCacheStub.DecreaseEntry() { Id = uniqueFile1 },
                new FileBlobCacheStub.DecreaseEntry() { Id = repeatedFileId }
            }), Is.True);
            m_FileBlobCacheStub.Entries.Clear();

            // Ok, let's try one list time, this time we wont throw a wrench in the machine and it should work!
            var fetchedPayload = await payloadsManager.GetPayload(somePayloadId);
            Assert.That(fetchedPayload, Is.SameAs(payload));
            Assert.That(m_FileBlobCacheStub.CompareEntries(new [] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile1,    CompressedSize = 28, Size = 56 },
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile2,    CompressedSize = 280, Size = 560 },
            }), Is.True);
            m_FileBlobCacheStub.Entries.Clear();
        }

        [Test]
        public async Task LongFetch()
        {
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);

            Guid repeatedFileId = Guid.NewGuid();
            Guid uniqueFile1 = Guid.NewGuid();
            Guid uniqueFile2 = Guid.NewGuid();
            var payloadId = Guid.NewGuid();
            var payload = new Payload();
            payload.Files = new[] {
                new PayloadFile() {Path="someFile",        FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="someOtherFile",   FileBlob = uniqueFile1,    CompressedSize = 28, Size = 56},
                new PayloadFile() {Path="repeat/someFile", FileBlob = repeatedFileId, CompressedSize = 42, Size = 84},
                new PayloadFile() {Path="somethingElse",   FileBlob = uniqueFile2,    CompressedSize = 280, Size = 560}
            };

            TaskCompletionSource fetchTcs = new();
            int fetchCount = 0;
            payloadsManager.FetchFileCallback = async (_, _) =>
            {
                Interlocked.Increment(ref fetchCount);
                await fetchTcs.Task;
                return payload;
            };

            var getRequest1Task = payloadsManager.GetPayload(payloadId);
            var getRequest2Task = payloadsManager.GetPayload(payloadId);

            await Task.Delay(50);

            Assert.That(getRequest1Task.IsCompleted, Is.False);
            Assert.That(getRequest2Task.IsCompleted, Is.False);
            fetchTcs.SetResult();

            var resultFromGet1 = await getRequest1Task;
            var resultFromGet2 = await getRequest2Task;

            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(resultFromGet1, Is.SameAs(payload));
            Assert.That(resultFromGet2, Is.SameAs(payload));
            Assert.That(m_FileBlobCacheStub.CompareEntries(new [] {
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile1,    CompressedSize = 28, Size = 56 },
                new FileBlobCacheStub.IncreaseEntry() { Id = repeatedFileId, CompressedSize = 42, Size = 84 },
                new FileBlobCacheStub.IncreaseEntry() { Id = uniqueFile2,    CompressedSize = 280, Size = 560 },
            }), Is.True);
            m_FileBlobCacheStub.Entries.Clear();
        }

        [Test]
        public async Task LongFailingFetch()
        {
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerStub, storageFolder, m_FileBlobCacheStub);

            TaskCompletionSource fetchTcs = new();
            int fetchCount = 0;
            payloadsManager.FetchFileCallback = async (_, _) =>
            {
                Interlocked.Increment(ref fetchCount);
                await fetchTcs.Task;
                throw new TestException();
            };

            var payloadId = Guid.NewGuid();
            var getRequest1Task = payloadsManager.GetPayload(payloadId);
            var getRequest2Task = payloadsManager.GetPayload(payloadId);

            await Task.Delay(50);

            Assert.That(getRequest1Task.IsCompleted, Is.False);
            Assert.That(getRequest2Task.IsCompleted, Is.False);
            fetchTcs.SetResult();

            Assert.That(async () => await getRequest1Task, Throws.TypeOf<TestException>());
            Assert.That(async () => await getRequest2Task, Throws.TypeOf<TestException>());
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);

            // However we should be able to try again once everything is failed
            payloadsManager.FetchFileCallback = (_, _) => Task.FromResult(new Payload());

            var result = await payloadsManager.GetPayload(payloadId);
            Assert.That(result.Files, Is.Empty);
            Assert.That(m_FileBlobCacheStub.Entries, Is.Empty);
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "PayloadsManagerTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        static bool ComparePayload(Payload payload1, Payload payload2)
        {
            return JsonSerializer.Serialize(payload1) == JsonSerializer.Serialize(payload2);
        }

        List<string> m_StorageFolders = new();
        LoggerStub m_LoggerStub = new();
        FileBlobCacheStub m_FileBlobCacheStub = new FileBlobCacheStub();
    }
}
