using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class PrepareCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            m_MissionControlStub.Start();
        }

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
                catch { }
            }

            m_MissionControlStub.Stop();
        }

        [Test]
        public async Task PrepareFolder()
        {
            await StartAndConfigureProcess();

            var payloadId = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payloadId, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payloadId, "file2.txt", fileBlob2, "File2 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payloadId },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payloadId}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/fileBlobs/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/fileBlobs/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")), Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")), Is.EqualTo("File2 content"));
        }

        [Test]
        public async Task PrepareMergedPayloads()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();
            var payload2 = Guid.NewGuid();
            var fileBlob4 = Guid.NewGuid();
            var fileBlob5 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt",         fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payload1, "file2.txt",         fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload1, "folder1/file2.txt", fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload1, "folder1/file3.txt", fileBlob3, "File3 content");
            m_MissionControlStub.AddFile(payload2, "file2.txt",         fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload2, "file4.txt",         fileBlob4, "File4 content");
            m_MissionControlStub.AddFile(payload2, "folder1/file2.txt", fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload2, "folder2/file5.txt", fileBlob5, "File5 content");
            m_MissionControlStub.AddFile(payload2, "folder3/file5.txt", fileBlob5, "File5 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1, payload2 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.Count(), Is.EqualTo(7)); // 2 payloads + 5 file blobs

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")),         Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file3.txt")), Is.EqualTo("File3 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file4.txt")),         Is.EqualTo("File4 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder2/file5.txt")), Is.EqualTo("File5 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder3/file5.txt")), Is.EqualTo("File5 content"));
        }

        [Test]
        public async Task IntraPayloadConflict()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob2, "File2 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);

            Assert.That(GetFilesOf(prepareCommand.Path), Is.Empty);
        }

        [Test]
        public async Task InterPayloadConflict()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var payload2 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payload2, "file1.txt", fileBlob2, "File2 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1, payload2 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/payloads/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/payloads/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);

            Assert.That(GetFilesOf(prepareCommand.Path), Is.Empty);
        }

        [Test]
        public async Task PayloadUnknownToMissionControl()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);

            Assert.That(GetFilesOf(prepareCommand.Path), Is.Empty);
        }

        [Test]
        public async Task PayloadConflict()
        {
            await StartAndConfigureProcess();

            // Cause some other conflict (other than unknown to MissionControl) in the server while asking for a
            // payload.  An easy way to force that is to have the same file blob in two different payloads with
            // different sizes.

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var payload2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payload2, "file1.txt", fileBlob1, "File1 content but too long", true);

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1, payload2 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/payloads/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri.StartsWith("api/v1/payloads/"), Is.True);
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);
        }

        [Test]
        public async Task FileBlobUnknownToMissionControl()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.RemoveFileBlob(fileBlob1);

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/fileBlobs/{fileBlob1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);
        }

        [Test]
        public async Task FileIsLocked()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            FileStream? lockedFile = null;
            var fetchFileCheckpoint = new MissionControlStubCheckpoint();
            m_MissionControlStub.AddFileCheckpoint(fileBlob1, fetchFileCheckpoint);
            var checkpointCompletedTask = fetchFileCheckpoint.WaitingOnCheckpoint.ContinueWith(t =>
            {
                Directory.CreateDirectory(prepareCommand.Path);
                lockedFile = File.Create(Path.Combine(prepareCommand.Path, "file1.txt"));
                fetchFileCheckpoint.UnblockCheckpoint();
            });

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            lockedFile?.Dispose();
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            Assert.That(checkpointCompletedTask.IsCompleted, Is.True);

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/fileBlobs/{fileBlob1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out _), Is.False);
        }

        [Test]
        public async Task PrepareInNonEmptyNewFolderWithLockedFileInPayload()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            // Create a file in the directory that is locked and cannot be cleaned.  That file is one that is in the payload,
            // so it should fail.
            Directory.CreateDirectory(prepareCommand.Path);
            using var lockedFile = File.Create(Path.Combine(prepareCommand.Path, "file1.txt"));

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.False);
        }

        [Test]
        public async Task PrepareInNonEmptyNewFolderWithLockedFileNotInPayload()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt", fileBlob1, "File1 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            // Create a file in the directory that is locked and cannot be cleaned.  That file is not in the payload,
            // so it should allow it and succeed (with a warning in the server console).
            Directory.CreateDirectory(prepareCommand.Path);
            using var lockedFile = File.Create(Path.Combine(prepareCommand.Path, "file2.txt"));

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.TryDequeue(out var historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/payloads/{payload1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.True);
            Assert.That(historyEntry, Is.Not.Null);
            Assert.That(historyEntry.Uri, Is.EqualTo($"api/v1/fileBlobs/{fileBlob1}"));
            Assert.That(m_MissionControlStub.History.TryDequeue(out historyEntry), Is.False);

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")), Is.EqualTo("File1 content"));
        }

        [Test]
        public async Task Incremental()
        {
            await StartAndConfigureProcess();

            var payload1 = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();
            var payload2 = Guid.NewGuid();
            var fileBlob4 = Guid.NewGuid();
            var fileBlob5 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payload1, "file1.txt",         fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payload1, "file2.txt",         fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload1, "folder1/file2.txt", fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload1, "folder1/file3.txt", fileBlob3, "File3 content");
            m_MissionControlStub.AddFile(payload2, "file2.txt",         fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload2, "file4.txt",         fileBlob4, "File4 content");
            m_MissionControlStub.AddFile(payload2, "folder1/file2.txt", fileBlob2, "File2 content");
            m_MissionControlStub.AddFile(payload2, "folder2/file5.txt", fileBlob5, "File5 content");
            m_MissionControlStub.AddFile(payload2, "folder3/file5.txt", fileBlob5, "File5 content");

            // Prepare payload1
            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payload1 },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.Count(), Is.EqualTo(4)); // 1 payloads + 3 file blobs
            m_MissionControlStub.History.Clear();

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")),         Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file3.txt")), Is.EqualTo("File3 content"));
            var file2LastWriteTime =        File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "file2.txt"));
            var folder1File2LastWriteTime = File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder1/file2.txt"));

            // Prepare payload2
            prepareCommand.PayloadIds = new[] { payload2 };

            response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.Count(), Is.EqualTo(3)); // 1 payloads + 2 file blobs
            m_MissionControlStub.History.Clear();

            Assert.That(File.Exists(     Path.Combine(prepareCommand.Path, "file1.txt")),         Is.False                   );
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo("File2 content"));
            Assert.That(File.Exists(     Path.Combine(prepareCommand.Path, "folder1/file3.txt")), Is.False                   );
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file4.txt")),         Is.EqualTo("File4 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder2/file5.txt")), Is.EqualTo("File5 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder3/file5.txt")), Is.EqualTo("File5 content"));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo(file2LastWriteTime));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo(folder1File2LastWriteTime));
            var file4LastWriteTime =        File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "file4.txt"));
            var folder2File5LastWriteTime = File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder2/file5.txt"));
            var folder3File5LastWriteTime = File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder3/file5.txt"));

            // Prepare both
            prepareCommand.PayloadIds = new[] { payload1, payload2 };

            response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History, Is.Empty); // Everything should already be cached

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")),         Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo("File2 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder1/file3.txt")), Is.EqualTo("File3 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file4.txt")),         Is.EqualTo("File4 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder2/file5.txt")), Is.EqualTo("File5 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "folder3/file5.txt")), Is.EqualTo("File5 content"));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "file2.txt")),         Is.EqualTo(file2LastWriteTime));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder1/file2.txt")), Is.EqualTo(folder1File2LastWriteTime));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "file4.txt")),         Is.EqualTo(file4LastWriteTime));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder2/file5.txt")), Is.EqualTo(folder2File5LastWriteTime));
            Assert.That(File.GetLastWriteTime(Path.Combine(prepareCommand.Path, "folder3/file5.txt")), Is.EqualTo(folder3File5LastWriteTime));
        }

        [Test]
        public async Task ErrorOnRemoveOfChangedFile()
        {
            await StartAndConfigureProcess();

            var payloadId = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payloadId, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payloadId, "file2.txt", fileBlob2, "File2 content");

            // First prepare
            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payloadId },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.Count(), Is.EqualTo(3)); // 1 payloads + 2 file blobs
            m_MissionControlStub.History.Clear();

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")), Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")), Is.EqualTo("File2 content"));

            // Touch a file so that it needs to be updated
            File.SetLastWriteTimeUtc(Path.Combine(prepareCommand.Path, "file2.txt"), DateTime.UtcNow);
            // And lock it so that it cannot be cleaned
            using var lockedFile = File.Open(Path.Combine(prepareCommand.Path, "file2.txt"), FileMode.Open);

            // Prepare again
            response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task StorageFolderStatusIsSaved()
        {
            var hangarBayFolder = await StartAndConfigureProcess();

            var payloadId = Guid.NewGuid();
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            m_MissionControlStub.AddFile(payloadId, "file1.txt", fileBlob1, "File1 content");
            m_MissionControlStub.AddFile(payloadId, "file2.txt", fileBlob2, "File2 content");

            var prepareCommand = new PrepareCommand()
            {
                Path = Path.Combine(GetTestTempFolder(), "PrepareInto"),
                PayloadIds = new[] { payloadId },
                PayloadSource = MissionControlStub.HttpListenerEndpoint
            };

            var response = await m_ProcessHelper.PostCommand(prepareCommand);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(m_MissionControlStub.History.Count(), Is.EqualTo(3)); // 1 payloads + 2 file blobs
            m_MissionControlStub.History.Clear();

            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file1.txt")), Is.EqualTo("File1 content"));
            Assert.That(File.ReadAllText(Path.Combine(prepareCommand.Path, "file2.txt")), Is.EqualTo("File2 content"));

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            var savedStorageFolderStatus = status.StorageFolders.First();
            Assert.That(savedStorageFolderStatus, Is.Not.Null);
            // Remark: The expected value below might change if for whatever reason the compression algorithm of the .
            // net framework changes.
            Assert.That(savedStorageFolderStatus.CurrentSize, Is.EqualTo(58));

            // Stop the server and restart it
            m_ProcessHelper.Stop();
            await m_ProcessHelper.Start(hangarBayFolder);

            // Look at the storage folder status, it should contain fileBlob1 and fileBlob2
            status = await m_ProcessHelper.GetStatus();
            Assert.That(status, Is.Not.Null);
            Assert.That(status.StorageFolders.Count(), Is.EqualTo(1));
            Assert.That(status.StorageFolders.First(), Is.EqualTo(savedStorageFolderStatus));
        }

        async Task<string> StartAndConfigureProcess(string folder = "")
        {
            if (string.IsNullOrEmpty(folder))
            {
                folder = GetTestTempFolder();
            }
            await m_ProcessHelper.Start(folder);

            var newConfig = await m_ProcessHelper.GetConfig();
            var storageFolder =
                new StorageFolderConfig() { Path = GetTestTempFolder(), MaximumSize = 10L * 1024 * 1024 * 1024 };
            newConfig.StorageFolders = new[] { storageFolder };

            var httpRet = await m_ProcessHelper.PutConfig(newConfig);
            Assert.That(httpRet, Is.Not.Null);
            Assert.That(httpRet.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            return folder;
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "PrepareCommandTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        string[] GetFilesOf(string path)
        {
            if (Directory.Exists(path))
            {
                var enumOptions = new EnumerationOptions();
                enumOptions.RecurseSubdirectories = true;
                return Directory.GetFiles(path, "*", enumOptions);
            }
            else
            {
                return new string[] { };
            }
        }

        HangarBayProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
        MissionControlStub m_MissionControlStub = new();
    }
}
