using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;
// ReSharper disable StructuredMessageTemplateProblem

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class PayloadsManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_FileBlobCacheMock = new(MockBehavior.Strict, m_LoggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
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

            m_LoggerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task FillSaveAndLoad()
        {
            Assert.That(m_FileBlobCacheMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock!.Object);

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

            // Get Payload1
            var sequence = new MockSequence();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28, 56));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));

            var resultingPayload1 = await payloadsManager.GetPayload(payload1Id, someCookie);
            Assert.That(resultingPayload1, Is.SameAs(payload1));
            Assert.That(fetchCallCount, Is.EqualTo(1));
            m_FileBlobCacheMock.Reset();

            // Get Payload2
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile2,    28, 56));

            var resultingPayload2 = await payloadsManager.GetPayload(payload2Id, someCookie);
            Assert.That(resultingPayload2, Is.SameAs(payload2));
            Assert.That(fetchCallCount, Is.EqualTo(2));
            m_FileBlobCacheMock.Reset();

            // Reload
            m_FileBlobCacheMock.Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
            m_FileBlobCacheMock.Setup(c => c.IncreaseUsageCount(uniqueFile1,    28, 56));
            m_FileBlobCacheMock.Setup(c => c.IncreaseUsageCount(uniqueFile2,    28, 56));

            var payloadsManagerNew = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock.Object);
            payloadsManagerNew.FetchFileCallback = payloadsManager.FetchFileCallback;
            m_FileBlobCacheMock.Verify(c => c.IncreaseUsageCount(repeatedFileId, 42, 84), Times.Exactly(3));
            m_FileBlobCacheMock.Verify(c => c.IncreaseUsageCount(uniqueFile1,    28, 56), Times.Once());
            m_FileBlobCacheMock.Verify(c => c.IncreaseUsageCount(uniqueFile2,    28, 56), Times.Once());
            m_FileBlobCacheMock.Reset();

            resultingPayload1 = await payloadsManagerNew.GetPayload(payload1Id, someCookie);
            Assert.That(resultingPayload1, Is.Not.SameAs(payload1)); // Since it got reloaded from json
            Assert.That(ComparePayload(resultingPayload1, payload1), Is.True);
            resultingPayload2 = await payloadsManagerNew.GetPayload(payload2Id, someCookie);
            Assert.That(resultingPayload1, Is.Not.SameAs(payload2)); // Since it got reloaded from json
            Assert.That(ComparePayload(resultingPayload2, payload2), Is.True);
            Assert.That(fetchCallCount, Is.EqualTo(2)); // Should not fetch again, everything was reloaded
        }

        class FakeException: Exception
        {
        }

        [Test]
        public async Task LoadErrors()
        {
            Assert.That(m_FileBlobCacheMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock!.Object);

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

            void PrepareMockForSequence()
            {
                var sequence = new MockSequence();
                m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
                m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28, 56));
                m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
            }

            PrepareMockForSequence();
            var resultingPayload1 = await payloadsManager.GetPayload(payloadId);
            Assert.That(resultingPayload1, Is.SameAs(payload));
            Assert.That(fetchCallCount, Is.EqualTo(1));
            m_FileBlobCacheMock.Reset();

            // Add an unrelated json file in the folder and try to load
            string unrelatedFilePath = Path.Combine(storageFolder, "Unrelated.json");
            await File.WriteAllTextAsync(unrelatedFilePath, "{}");

            PrepareMockForSequence();
            _ = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock.Object);
            m_LoggerMock.VerifyLog(l => l.LogWarning("Unexpected filename*", "Unrelated"));
            m_FileBlobCacheMock.Reset();
            File.Delete(unrelatedFilePath);

            // Add a .json file with non json content in it and try to load
            string badPayloadFilenameWoExt = Guid.NewGuid().ToString();
            string badPayloadFilename = badPayloadFilenameWoExt + ".json";
            string badPayloadPath = Path.Combine(storageFolder, badPayloadFilename);
            await File.WriteAllTextAsync(badPayloadPath, "This is not a json");

            PrepareMockForSequence();
            _ = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock.Object);
            m_LoggerMock.VerifyLog(l => l.LogWarning("Failed loading back*", badPayloadFilenameWoExt));
            m_FileBlobCacheMock.Reset();
            File.Delete(badPayloadPath);

            // Fake a loading problem (that would be cause by conflicting sizes for example) when loading back assets.
            var sequence = new MockSequence();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28, 56));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42, 84)).Throws<FakeException>();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.DecreaseUsageCount(uniqueFile1           ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.DecreaseUsageCount(repeatedFileId        ));
            _ = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock.Object);
            m_LoggerMock.VerifyLog(l => l.LogWarning("There was a problem processing files of*", payloadId.ToString()));
            m_FileBlobCacheMock.Reset();
            File.Delete(badPayloadPath);
        }

        class TestException: Exception
        {
        }

        [Test]
        public async Task FetchFailures()
        {
            Assert.That(m_FileBlobCacheMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock!.Object);

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
            payloadsManager.FetchFileCallback = (_, _) => Task.FromResult(payload);

            var sequence = new MockSequence();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28,  56 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile2,    280, 560)).Throws<FakeException>();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.DecreaseUsageCount(repeatedFileId          ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.DecreaseUsageCount(uniqueFile1             ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.DecreaseUsageCount(repeatedFileId          ));

            Assert.That(() => payloadsManager.GetPayload(somePayloadId), Throws.TypeOf<FakeException>());
            m_FileBlobCacheMock.Reset();

            // Ok, let's try one list time, this time we wont throw a wrench in the machine and it should work!
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28,  56 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile2,    280, 560));

            var fetchedPayload = await payloadsManager.GetPayload(somePayloadId);
            Assert.That(fetchedPayload, Is.SameAs(payload));
            m_FileBlobCacheMock.Reset();
        }

        [Test]
        public async Task LongFetch()
        {
            Assert.That(m_FileBlobCacheMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock!.Object);

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

            var sequence = new MockSequence();
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile1,    28,  56 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(repeatedFileId, 42,  84 ));
            m_FileBlobCacheMock.InSequence(sequence).Setup(c => c.IncreaseUsageCount(uniqueFile2,    280, 560));

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
            m_FileBlobCacheMock.Reset();
        }

        [Test]
        public async Task LongFailingFetch()
        {
            Assert.That(m_FileBlobCacheMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobCacheMock!.Object);

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

            // However we should be able to try again once everything is failed
            payloadsManager.FetchFileCallback = (_, _) => Task.FromResult(new Payload());

            var result = await payloadsManager.GetPayload(payloadId);
            Assert.That(result.Files, Is.Empty);
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
        Mock<ILogger> m_LoggerMock = new();
        Mock<FileBlobCache>? m_FileBlobCacheMock;
    }
}
