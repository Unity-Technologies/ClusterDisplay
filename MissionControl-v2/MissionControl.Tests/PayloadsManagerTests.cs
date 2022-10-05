using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class PayloadsManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
            m_FileBlobsManagerMock = new(MockBehavior.Strict, m_LoggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string filePath in m_FileToClearAttributes)
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
            m_FileToClearAttributes.Clear();

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
        public async Task AddRemovePayload()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid repeatedFileId = Guid.NewGuid();
            Guid uniqueFile1 = Guid.NewGuid();
            Guid uniqueFile2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("someFile",        repeatedFileId, 42, 84),
                new PayloadFile("someOtherFile",   uniqueFile1,    28, 56),
                new PayloadFile("repeat/someFile", repeatedFileId, 42, 84),
                new PayloadFile("last file",       uniqueFile2,    82, 24),
            });
            var payloadFile = Path.Combine(storageFolder, $"{payloadId}.json");

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(repeatedFileId));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(uniqueFile1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(repeatedFileId));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(uniqueFile2));

            await payloadsManager.AddPayloadAsync(payloadId, payload);
            Assert.That(File.Exists(payloadFile), Is.True);

            // Can we get it back
            var gotBackPayload = payloadsManager.GetPayload(payloadId);
            Assert.That(gotBackPayload, Is.SameAs(payload));

            // Remove it
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(repeatedFileId)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(uniqueFile1)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(repeatedFileId)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(uniqueFile2)).Returns(Task.CompletedTask);

            await payloadsManager.RemovePayloadAsync(payloadId);
            Assert.That(File.Exists(payloadFile), Is.False);
            Assert.Throws<KeyNotFoundException>(() => payloadsManager.GetPayload(payloadId));
        }

        [Test]
        public void AddMissingBlob()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });
            var payloadFile = Path.Combine(storageFolder, $"{payloadId}.json");

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2)).Throws<KeyNotFoundException>();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(file1)).Returns(Task.CompletedTask);

            Assert.ThrowsAsync<KeyNotFoundException>(() => payloadsManager.AddPayloadAsync(payloadId, payload));

            // There should be any trace of the payload in the manager
            Assert.That(File.Exists(payloadFile), Is.False);
            Assert.Throws<KeyNotFoundException>(() => payloadsManager.GetPayload(payloadId));
        }

        [Test]
        public async Task AddTwice()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2));

            await payloadsManager.AddPayloadAsync(payloadId, payload);

            // Add it again
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(file2)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(file1)).Returns(Task.CompletedTask);

            Assert.ThrowsAsync<ArgumentException>(() => payloadsManager.AddPayloadAsync(payloadId, payload));
        }

        [Test]
        public void AddFailSerialize()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });
            var payloadFile = Path.Combine(storageFolder, $"{payloadId}.json");

            // Create a file that will make writing to the payload's json fail
            File.WriteAllText(payloadFile, "Bad content");
            File.SetAttributes(payloadFile, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(payloadFile);

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(file2)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.DecreaseFileBlobReferenceAsync(file1)).Returns(Task.CompletedTask);

            Assert.ThrowsAsync<UnauthorizedAccessException>(() => payloadsManager.AddPayloadAsync(payloadId, payload));
            m_LoggerMock.VerifyLog(l => l.LogError("Fail to delete incomplete payload file while serializing*"));

            // There should be any trace of the payload in the manager
            Assert.That(File.ReadAllText(payloadFile), Is.EqualTo("Bad content"));
            Assert.Throws<KeyNotFoundException>(() => payloadsManager.GetPayload(payloadId));
        }

        [Test]
        public void RemoveMissing()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);
            Assert.ThrowsAsync<KeyNotFoundException>(() => payloadsManager.RemovePayloadAsync(Guid.NewGuid()));
        }

        [Test]
        public async Task RemoveFailDeleteFile()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });
            var payloadFile = Path.Combine(storageFolder, $"{payloadId}.json");

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2));

            await payloadsManager.AddPayloadAsync(payloadId, payload);
            Assert.That(File.Exists(payloadFile), Is.True);

            // Lock the file
            File.SetAttributes(payloadFile, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(payloadFile);

            // Remove it
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => payloadsManager.RemovePayloadAsync(payloadId));

            // Ensure everything still ok (since the removal failed)
            Assert.That(File.Exists(payloadFile), Is.True);
            var gotPayload = payloadsManager.GetPayload(payloadId);
            Assert.That(gotPayload, Is.SameAs(payload));
        }

        [Test]
        public async Task RemoveContinuesIfFileBlobsMissing()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();

            var payloadId = Guid.NewGuid();
            Payload payload = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });
            var payloadFile = Path.Combine(storageFolder, $"{payloadId}.json");

            // Add it
            var sequence = new MockSequence();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2));

            await payloadsManager.AddPayloadAsync(payloadId, payload);
            Assert.That(File.Exists(payloadFile), Is.True);

            // Remove it
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file1)).Throws<KeyNotFoundException>();
            m_FileBlobsManagerMock.InSequence(sequence).Setup(c => c.IncreaseFileBlobReference(file2)).Throws<InvalidOperationException>();

            await payloadsManager.RemovePayloadAsync(payloadId);

            m_LoggerMock.VerifyLog(l => l.LogWarning("Removal of payload {PayloadId}*{FileBlobId} was missing*",
                payloadId, file1));
            m_LoggerMock.VerifyLog(l => l.LogWarning("Removal of payload {PayloadId}*{FileBlobId} was missing*",
                payloadId, file2));

            // Ensure everything is removed
            Assert.That(File.Exists(payloadFile), Is.False);
            Assert.Throws<KeyNotFoundException>(() => payloadsManager.GetPayload(payloadId));
        }

        [Test]
        public async Task Load()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var storageFolder = GetNewStorageFolder();
            var payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            // Create some payload and add them to the manager (so that there is something to load back).
            Guid file1 = Guid.NewGuid();
            Guid file2 = Guid.NewGuid();
            Guid file3 = Guid.NewGuid();

            var payload1Id = Guid.NewGuid();
            Payload payload1 = new (new[] {
                new PayloadFile("file1", file1, 42, 84),
                new PayloadFile("file2", file2, 28, 56)
            });
            var payload2Id = Guid.NewGuid();
            Payload payload2 = new (new[] {
                new PayloadFile("file2", file2, 28, 56),
                new PayloadFile("file3", file3, 28, 42)
            });

            m_FileBlobsManagerMock.Setup(c => c.IncreaseFileBlobReference(file1));
            m_FileBlobsManagerMock.Setup(c => c.IncreaseFileBlobReference(file2));
            m_FileBlobsManagerMock.Setup(c => c.IncreaseFileBlobReference(file3));

            await payloadsManager.AddPayloadAsync(payload1Id, payload1);
            await payloadsManager.AddPayloadAsync(payload2Id, payload2);

            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file1), Times.Once());
            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file2), Times.Exactly(2));
            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file3), Times.Once());

            // Create some "bad files" that should generate log messages but otherwise loading should continue.
            await File.WriteAllTextAsync(Path.Combine(storageFolder, "BadFilename.json"), "BadContent");
            var badFileId1 = Guid.NewGuid();
            await File.WriteAllTextAsync(Path.Combine(storageFolder, $"{badFileId1}.json"), "BadContent");
            m_FileBlobsManagerMock.Setup(c => c.IncreaseFileBlobReference(file3)).Throws<KeyNotFoundException>();

            // Create a new PayloadsManager from the previous folder, it should load back everything
            payloadsManager = new PayloadsManager(m_LoggerMock.Object, storageFolder, m_FileBlobsManagerMock!.Object);

            m_LoggerMock.VerifyLog(l => l.LogWarning("Unexpected filename ({FileNameWoExtension}.json) encountered*",
                "BadFilename"));
            m_LoggerMock.VerifyLog(l => l.LogError("Failed loading back {FileNameWoExtension}.json*",
                badFileId1.ToString()));
            m_LoggerMock.VerifyLog(l => l.LogError("There was a problem increasing usage count of {Path} to {BlobId} " +
                "of {FileNameWoExtension}.json, *", "file3", file3, payload2Id.ToString()));

            var loadedPayload1 = payloadsManager.GetPayload(payload1Id);
            Assert.That(loadedPayload1, Is.Not.SameAs(payload1));
            Assert.That(loadedPayload1, Is.EqualTo(payload1));
            var loadedPayload2 = payloadsManager.GetPayload(payload2Id);
            Assert.That(loadedPayload2, Is.Not.SameAs(payload2));
            Assert.That(loadedPayload2, Is.EqualTo(payload2));

            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file1), Times.Exactly(2));
            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file2), Times.Exactly(4));
            m_FileBlobsManagerMock.Verify(c => c.IncreaseFileBlobReference(file3), Times.Exactly(2));
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "PayloadsManagerTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        List<string> m_StorageFolders = new();
        List<string> m_FileToClearAttributes = new();
        Mock<ILogger> m_LoggerMock = new();
        Mock<FileBlobsManager>? m_FileBlobsManagerMock;
    }
}
