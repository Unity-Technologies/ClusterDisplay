using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class FileBlobsManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
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
            m_StorageFolders.Clear();

            m_LoggerMock.VerifyNoOtherCalls();
        }

        [Test]
        public void AddNewStorageFolder()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });
        }

        [Test]
        public async Task AddStorageFolderWithContent()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Initial create of the storage folder
            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });

            // Add some content
            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

            // Serialize storage folder
            manager.PersistStorageFolderStates();

            // Create a new manager and add storage folder back
            manager = new FileBlobsManager(m_LoggerMock.Object);
            manager.AddStorageFolder(folderConfig);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

            manager.IncreaseFileBlobReference(file1Id);
            manager.IncreaseFileBlobReference(file2Id);
            Assert.Throws<KeyNotFoundException>(() => manager.IncreaseFileBlobReference(Guid.NewGuid()));

            // Try adding a file that is already present, it should find it back
            file1Content.Position = 0;
            var file1Take2Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Take2Id, Is.EqualTo(file1Id));
        }

        [Test]
        public async Task AddStorageFolderWithContentConflicts()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create of the first storage folder
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 0, MaximumSize = 1000 } });

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 86, MaximumSize = 1000 } });

            manager.PersistStorageFolderStates();

            // Create the second storage folder.  Start from the first one and add a new file
            manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            CloneDirectory(folder1Config.Path, folder2Config.Path);
            manager.AddStorageFolder(folder2Config);

            string file3ContentString = "This is yet another file content";
            await using var file3Content = CreateStreamFromString(file3ContentString);
            var file3Md5 = ComputeMd5Guid(file3Content);
            var file3Id = await manager.AddFileBlobAsync(file3Content, file3Content.Length, file3Md5);
            Assert.That(file3Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file3Id, Is.Not.EqualTo(file1Id));
            Assert.That(file3Id, Is.Not.EqualTo(file2Id));

            manager.PersistStorageFolderStates();

            // Now let's try to add them both to the same manager
            manager = new FileBlobsManager(m_LoggerMock.Object);
            manager.AddStorageFolder(folder1Config);
            Assert.Throws<ArgumentException>(() => manager.AddStorageFolder(folder2Config));
            m_LoggerMock.VerifyLog(l => l.LogError("Conflict found while indexing files*"));

            // Verify the failure of adding folder2Config did not had any negative impacts
            manager.IncreaseFileBlobReference(file1Id);
            manager.IncreaseFileBlobReference(file2Id);
            Assert.Throws<KeyNotFoundException>(() => manager.IncreaseFileBlobReference(Guid.NewGuid()));

            file1Content.Position = 0;
            var file1Take2Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Take2Id, Is.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 86, MaximumSize = 1000 } });
        }

        [Test]
        public async Task KillZombiesWhenAddingStorage()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Initial create of the storage folder
            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });

            // Add some content
            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

            // Serialize storage folder
            manager.PersistStorageFolderStates();

            // Create zombies
            string zombie1Path = GetBlobPath(folderConfig.Path, Guid.NewGuid());
            Directory.CreateDirectory(Path.GetDirectoryName(zombie1Path)!);
            await File.WriteAllTextAsync(zombie1Path, "Zombie 1 content");
            string zombie2Path = Path.Combine(folderConfig.Path, "Some other file.txt");
            await File.WriteAllTextAsync(zombie2Path, "Zombie 2 content, brain...  Need more brain!!!");
            string zombie3Path = GetBlobPath(folderConfig.Path, Guid.NewGuid());
            Directory.CreateDirectory(Path.GetDirectoryName(zombie3Path)!);
            await File.WriteAllTextAsync(zombie3Path, "Zombie 3 content, I'm invincible!!!!"); // 36 bytes
            File.SetAttributes(zombie3Path, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(zombie3Path);
            string zombie4Path = Path.Combine(folderConfig.Path, "EternalZombie.txt");
            await File.WriteAllTextAsync(zombie4Path, "Zombie 4 content that will stay present until the end of times!"); // 63 bytes
            File.SetAttributes(zombie4Path, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(zombie4Path);

            // Create a new manager and add storage folder back, should kill zombies (or at least, try to)
            manager = new FileBlobsManager(m_LoggerMock.Object);
            manager.AddStorageFolder(folderConfig);

            m_LoggerMock.VerifyLog(l => l.LogError("Failed to delete {File}", zombie3Path));
            m_LoggerMock.VerifyLog(l => l.LogError("Failed to delete {File}", zombie4Path));

            Assert.That(File.Exists(zombie1Path), Is.False);
            Assert.That(File.Exists(zombie2Path), Is.False);
            Assert.That(File.Exists(zombie3Path), Is.True);
            Assert.That(File.Exists(zombie4Path), Is.True);

            // Test empty sub-folders have been cleaned
            Assert.That(Directory.Exists(Path.GetDirectoryName(zombie1Path)), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(zombie3Path)), Is.True);

            // And status of the storage up to date
            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 185, ZombiesSize = 99,
                    MaximumSize = 1000 } });
        }

        [Test]
        public void AddStorageFolderErrors()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Double add should fail
            Assert.That(() => manager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // Double add through a different path.
            // We can get through this case through symbolic links or case insensitive filenames.  Since Windows
            // requires an elevated privilege to create symbolic links and most file systems under windows are case
            // insensitive let's simply change the case of the folder.
            string aliasPath = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                aliasPath = folderConfig.Path.ToLower();
                Assert.That(aliasPath, Is.Not.EqualTo(folderConfig.Path));
            }
            else
            {
                Assert.Fail("TODO");
            }
            var aliasConfig = new StorageFolderConfig();
            aliasConfig.Path = aliasPath;
            aliasConfig.MaximumSize = folderConfig.MaximumSize;
            Assert.That(() => manager.AddStorageFolder(aliasConfig), Throws.TypeOf<ArgumentException>());

            // Same for equivalent path
            folderConfig.Path = Path.Combine(folderConfig.Path, "Patate", "..");
            Assert.That(() => manager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // And try a non empty folder
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            var filePath = Path.Combine(folderConfig.Path, "SomeFile.txt");
            File.WriteAllText(filePath, "Some content");
            Assert.That(() => manager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task AddSmallFileBlob()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

            // Try to add them back again (should get the same ids)
            string[] hexContents = new[] { file1ContentString, file2ContentString };
            Guid[] expectedIds = new[] { file1Id, file2Id };
            for (int i = 0; i < hexContents.Length; i++)
            {
                await using var fileContent = CreateStreamFromString(hexContents[i]);
                var md5 = ComputeMd5Guid(fileContent);
                var blobId = await manager.AddFileBlobAsync(fileContent, fileContent.Length, md5);
                Assert.That(blobId, Is.EqualTo(expectedIds[i]));
            }
        }

        [Test]
        public async Task AddBigFileBlob()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            const int bigContentLength = 10 * 1024 * 1024;

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = (bigContentLength + 1024) * 3; // + 1024 because the bytes we generate does not compress well and the compressed file is larger than the uncompressed one
            manager.AddStorageFolder(folderConfig);

            byte[] file1ContentBytes = GetRandomBytes(bigContentLength);
            await using var file1Content = CreateStreamFromBytes(file1ContentBytes);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            byte[] file2ContentBytes = new byte[]{ };
            Guid file2Md5 = file1Md5;
            while (file2Md5 == file1Md5)
            {
                file2ContentBytes = GetRandomBytes(bigContentLength);
                file2Md5 = ComputeMd5Guid(file2ContentBytes);
            }

            await using MemoryStream file2Content = new(file2ContentBytes);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            // Try to add them back again (should get the same ids)
            byte[][] hexContents = new[] { file1ContentBytes, file2ContentBytes };
            Guid[] expectedIds = new[] { file1Id, file2Id };
            for (int i = 0; i < hexContents.Length; i++)
            {
                await using var fileContent = CreateStreamFromBytes(hexContents[i]);
                var md5 = ComputeMd5Guid(fileContent);
                var blobId = await manager.AddFileBlobAsync(fileContent, fileContent.Length, md5);
                Assert.That(blobId, Is.EqualTo(expectedIds[i]));
            }
        }

        [Test]
        public async Task AddClashingMd5FileBlob()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Add two clashing files (two different content with the same md5 checksum)

            await using var file1Content = CreateStreamFromHexString(k_CollisionFile1ContentHex);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 151, MaximumSize = 1000 } });

            await using var file2Content = CreateStreamFromHexString(k_CollisionFile2ContentHex);
            var file2Md5 = ComputeMd5Guid(file2Content);
            Assert.That(file2Md5, Is.EqualTo(file1Md5));
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 302, MaximumSize = 1000 } });

            // Try to add them back again (should get the same ids)
            string[] hexContents = new[] { k_CollisionFile1ContentHex, k_CollisionFile2ContentHex };
            Guid[] expectedIds = new[] { file1Id, file2Id };
            for (int i = 0; i < hexContents.Length; i++)
            {
                await using var fileContent = CreateStreamFromHexString(hexContents[i]);
                var md5 = ComputeMd5Guid(fileContent);
                var blobId = await manager.AddFileBlobAsync(fileContent, fileContent.Length, md5);
                Assert.That(blobId, Is.EqualTo(expectedIds[i]));
            }
        }

        [Test]
        public async Task ConcurrentAddSameMd5()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Start adding the first file (but voluntarily block concluding the stream providing the file).
            byte[] file1Content = Convert.FromHexString(k_CollisionFile1ContentHex);
            using ClientServerStream file1Streams = new();
            int file1FirstChunkSize = file1Content.Length / 2;
            await file1Streams.WriteStream.WriteAsync(file1Content, 0, file1FirstChunkSize); // Only write the first half
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Task = manager.AddFileBlobAsync(file1Streams.ReadStream, file1Content.Length, file1Md5);

            // AddFileBlobAsync Should add an entry in the storage folder of the size of the uncompressed file,
            // so wait for the storage space to increase.
            Assert.That(manager.GetStorageFolderStatus().Length, Is.EqualTo(1));
            var maxWaitTime = Stopwatch.StartNew();
            while (manager.GetStorageFolderStatus()[0].CurrentSize == 0 && maxWaitTime.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50);
            }
            Assert.That(manager.GetStorageFolderStatus()[0].CurrentSize, Is.EqualTo(file1Content.Length));
            await Task.Delay(100); // In case file1Task would be able to complete
            Assert.That(file1Task.IsCompleted, Is.False);

            // Start adding the second file (provide all the content ready to be used)
            byte[] file2Content = Convert.FromHexString(k_CollisionFile2ContentHex);
            await using MemoryStream file2Stream = new(file2Content);
            var file2Md5 = ComputeMd5Guid(file2Content);
            Assert.That(file2Md5, Is.EqualTo(file1Md5));
            var file2Task = manager.AddFileBlobAsync(file2Stream, file2Content.Length, file2Md5);

            // It should in theory block and not consume anything, so wait 100 ms just to be sure it really does
            // nothing.
            await Task.Delay(100); // In case file1Task would be able to complete
            Assert.That(file2Stream.Position, Is.Zero);
            Assert.That(file2Task.IsCompleted, Is.False);

            // Unblock by concluding content for file1
            await file1Streams.WriteStream.WriteAsync(file1Content, file1FirstChunkSize, file1Content.Length - file1FirstChunkSize);
            file1Streams.WriteStream.Close();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(file1Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file1Task)); // Or else await timed out -> AddFileBlobAsync is stuck
            awaitTask = await Task.WhenAny(file2Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file2Task)); // Or else await timed out -> AddFileBlobAsync is stuck

            var file1Id = file1Task.Result;
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));
            var file2Id = file2Task.Result;
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));
        }

        [Test]
        public async Task AddSameMd5WhileLocked()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Start adding the first file
            await using var file1Stream = CreateStreamFromHexString(k_CollisionFile1ContentHex);
            var file1Md5 = ComputeMd5Guid(file1Stream);
            var file1Id = await manager.AddFileBlobAsync(file1Stream, file1Stream.Length, file1Md5);

            // Lock the file
            using (await manager.LockFileBlob(file1Id))
            {
                // Add the second file with the same MD5 checksum
                await using var file2Stream = CreateStreamFromHexString(k_CollisionFile2ContentHex);
                var file2Md5 = ComputeMd5Guid(file2Stream);
                Assert.That(file2Md5, Is.EqualTo(file1Md5));
                var file2Id = await manager.AddFileBlobAsync(file2Stream, file2Stream.Length, file2Md5);

                Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(file2Id, Is.Not.EqualTo(file1Id));

                // Add the first file again
                file1Stream.Position = 0;
                var file1IdTake2 = await manager.AddFileBlobAsync(file1Stream, file1Stream.Length, file1Md5);
                Assert.That(file1IdTake2, Is.EqualTo(file1Id));
            }
        }

        [Test]
        public async Task ConcurrentAddDifferentMd5()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Start adding the first file (but voluntarily block concluding the stream providing the file).
            byte[] file1Content = GetRandomBytes(128);
            using ClientServerStream file1Streams = new();
            int file1FirstChunkSize = 32;
            await file1Streams.WriteStream.WriteAsync(file1Content, 0, file1FirstChunkSize); // Only write the first half
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Task = manager.AddFileBlobAsync(file1Streams.ReadStream, file1Content.Length, file1Md5);

            // AddFileBlobAsync Should add an entry in the storage folder of the size of the uncompressed file,
            // so wait for the storage space to increase.
            Assert.That(manager.GetStorageFolderStatus().Length, Is.EqualTo(1));
            var maxWaitTime = Stopwatch.StartNew();
            while (manager.GetStorageFolderStatus()[0].CurrentSize == 0 && maxWaitTime.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50);
            }
            Assert.That(manager.GetStorageFolderStatus()[0].CurrentSize, Is.EqualTo(file1Content.Length));
            await Task.Delay(100); // In case file1Task would be able to complete
            Assert.That(file1Task.IsCompleted, Is.False);

            // Start adding the second file (provide all the content ready to be used)
            byte[] file2Content = new byte[]{ };
            Guid file2Md5 = file1Md5;
            while (file2Md5 == file1Md5)
            {
                file2Content = GetRandomBytes(128);
                file2Md5 = ComputeMd5Guid(file2Content);
            }

            await using MemoryStream file2Stream = new(file2Content);
            var file2Task = manager.AddFileBlobAsync(file2Stream, file2Content.Length, file2Md5);

            // It shouldn't block as it is a different md5 checksum
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(file2Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file2Task)); // Or else await timed out -> AddFileBlobAsync is stuck

            // Unblock by concluding content for file1
            await file1Streams.WriteStream.WriteAsync(file1Content, file1FirstChunkSize, file1Content.Length - file1FirstChunkSize);
            file1Streams.WriteStream.Close();
            awaitTask = await Task.WhenAny(file1Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file1Task)); // Or else await timed out -> AddFileBlobAsync is stuck

            var file1Id = file1Task.Result;
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));
            var file2Id = file2Task.Result;
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));
        }

        [Test]
        public async Task AddSameContentMissingFromStorage()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Add the file
            string fileContentString = "This is the file content";
            await using var fileContent = CreateStreamFromString(fileContentString);
            var fileMd5 = ComputeMd5Guid(fileContent);
            var file1Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });

            // Delete it from the storage folder (in fact, this is the only file it should contain, so let's delete the
            // complete storage folder!
            Directory.Delete(folderConfig.Path, true);
            Directory.CreateDirectory(folderConfig.Path);

            // Add another file with the same content
            fileContent.Position = 0;
            var file2Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            m_LoggerMock.VerifyLog(l => l.LogWarning("Failed to read from blob {Id}*", file1Id));
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 84, MaximumSize = 1000 } });

            // Try to add them back again (without deleting from storage this time), should get the same id
            fileContent.Position = 0;
            var file3Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            m_LoggerMock.VerifyLog(l => l.LogWarning("Failed to read from blob {Id}*", file1Id));
            Assert.That(file3Id, Is.EqualTo(file2Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 84, MaximumSize = 1000 } });
        }

        [Test]
        public async Task AddSameContentStorageShorter()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Add the file
            string fileContentString = "This is the file content";
            await using var fileContent = CreateStreamFromString(fileContentString);
            var fileMd5 = ComputeMd5Guid(fileContent);
            var file1Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });

            // Hack the content of the file to make it shorter.
            string filePath = GetBlobPath(folderConfig.Path, file1Id);
            WriteCompressedContent(filePath, fileContentString.Substring(0, 10));

            // Add another file with the same content
            fileContent.Position = 0;
            var file2Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            m_LoggerMock.VerifyLog(l => l.LogWarning("FileBlob {Id} was shorter than expected " +
                "({Actual} vs {Expected})", file1Id, It.IsAny<long>(), It.IsAny<long>() ));
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 84, MaximumSize = 1000 } });
        }

        [Test]
        public async Task AddSameContentStorageLonger()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Add the file
            string fileContentString = "This is the file content";
            await using var fileContent = CreateStreamFromString(fileContentString);
            var fileMd5 = ComputeMd5Guid(fileContent);
            var file1Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });

            // Hack the content of the file to make it shorter.
            string filePath = GetBlobPath(folderConfig.Path, file1Id);
            WriteCompressedContent(filePath, fileContentString + fileContentString);

            // Add another file with the same content
            fileContent.Position = 0;
            var file2Id = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            m_LoggerMock.VerifyLog(l => l.LogWarning("FileBlob {Id} was longer than expected", file1Id));
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 84, MaximumSize = 1000 } });
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public async Task ExceptionWhileAdding(bool failDelete)
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Start adding the first file (but voluntarily block concluding the stream providing the file).
            byte[] file1Content = Convert.FromHexString(k_CollisionFile1ContentHex);
            using ClientServerStream file1Streams = new();
            int file1FirstChunkSize = file1Content.Length / 2;
            await file1Streams.WriteStream.WriteAsync(file1Content, 0, file1FirstChunkSize); // Only write the first half
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Task = manager.AddFileBlobAsync(file1Streams.ReadStream, file1Content.Length, file1Md5);

            // AddFileBlobAsync Should add an entry in the storage folder of the size of the uncompressed file,
            // so wait for the storage space to increase.
            Assert.That(manager.GetStorageFolderStatus().Length, Is.EqualTo(1));
            var maxWaitTime = Stopwatch.StartNew();
            while (manager.GetStorageFolderStatus()[0].CurrentSize == 0 && maxWaitTime.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50);
            }
            Assert.That(manager.GetStorageFolderStatus()[0].CurrentSize, Is.EqualTo(file1Content.Length));
            await Task.Delay(100); // In case file1Task would be able to complete
            Assert.That(file1Task.IsCompleted, Is.False);

            // Start adding the second file (provide all the content ready to be used)
            byte[] file2Content = Convert.FromHexString(k_CollisionFile2ContentHex);
            await using MemoryStream file2Stream = new(file2Content);
            var file2Md5 = ComputeMd5Guid(file2Content);
            Assert.That(file2Md5, Is.EqualTo(file1Md5));
            var file2Task = manager.AddFileBlobAsync(file2Stream, file2Content.Length, file2Md5);

            // It should in theory block and not consume anything, so wait 100 ms just to be sure it really does
            // nothing.
            await Task.Delay(100); // In case file1Task would be able to complete
            Assert.That(file2Stream.Position, Is.Zero);
            Assert.That(file2Task.IsCompleted, Is.False);

            // Force the delete of the file being compressed to fail by making the file read only
            long zombiesSize = 0;
            if (failDelete)
            {
                string? beingCompressedPath = Directory.GetFiles(folderConfig.Path, "*",
                    new EnumerationOptions() { RecurseSubdirectories = true }).FirstOrDefault();
                if (!string.IsNullOrEmpty(beingCompressedPath))
                {
                    File.SetAttributes(beingCompressedPath, FileAttributes.ReadOnly);
                    m_FileToClearAttributes.Add(beingCompressedPath);
                    zombiesSize = 151;
                }
            }

            // Cause the add of the first file to fail by disposing of the streams (while they are in use)
            await file1Streams.ReadStream.DisposeAsync();
            await file1Streams.WriteStream.WriteAsync(file1Content, file1FirstChunkSize, file1Content.Length - file1FirstChunkSize);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(file1Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file1Task)); // Or else await timed out -> AddFileBlobAsync is stuck
            Assert.That(awaitTask.IsFaulted, Is.True);
            awaitTask = await Task.WhenAny(file2Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file2Task)); // Or else await timed out -> AddFileBlobAsync is stuck

            if (failDelete)
            {
                m_LoggerMock.VerifyLog(l => l.LogError(It.IsAny<UnauthorizedAccessException>(),
                    "Failed to delete compressed file*"));
            }

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 151 + zombiesSize,
                    ZombiesSize = zombiesSize, MaximumSize = 1000 } });
            var files = Directory.GetFiles(folderConfig.Path, "*", new EnumerationOptions() { RecurseSubdirectories = true });
            Assert.That(files.Length, Is.EqualTo(failDelete ? 2: 1));
        }

        [Test]
        public async Task CancelWhileAdding()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            // Add the file but it takes a long time reading it...
            byte[] file1Content = Convert.FromHexString(k_CollisionFile1ContentHex);
            using ClientServerStream file1Streams = new();
            int file1FirstChunkSize = file1Content.Length / 2;
            await file1Streams.WriteStream.WriteAsync(file1Content, 0, file1FirstChunkSize); // Only write the first half
            var file1Md5 = ComputeMd5Guid(file1Content);
            CancellationTokenSource cancellationTokenSource = new();
            var file1Task = manager.AddFileBlobAsync(file1Streams.ReadStream, file1Content.Length, file1Md5,
                cancellationTokenSource.Token);

            // ReSharper disable once MethodSupportsCancellation -> No need to cancel the Delay
            await Task.Delay(100); // In case decreaseTask would be able to complete
            Assert.That(file1Task.IsCompleted, Is.False);

            // Cancel
            cancellationTokenSource.Cancel();

            // Wait add to be over (and canceled)
            // ReSharper disable once MethodSupportsCancellation -> No need to cancel the Delay
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(file1Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file1Task)); // Or else await timed out
            Assert.That(file1Task.IsCanceled, Is.True);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });
        }

        [Test]
        public async Task DecreaseReferenceCount()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            string fileContentString = "This is the file content";
            await using var fileContent = CreateStreamFromString(fileContentString);
            var fileMd5 = ComputeMd5Guid(fileContent);
            var fileId = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            Assert.That(fileId, Is.Not.EqualTo(Guid.Empty));

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });

            Assert.ThrowsAsync<KeyNotFoundException>(() => manager.DecreaseFileBlobReferenceAsync(Guid.NewGuid()));

            await manager.DecreaseFileBlobReferenceAsync(fileId);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });
            string? filesOfStorage = Directory.GetFiles(folderConfig.Path, "*",
                    new EnumerationOptions() { RecurseSubdirectories = true }).FirstOrDefault();
            Assert.That(filesOfStorage, Is.Null);
        }

        [Test]
        public async Task DecreaseReferenceCountOfUnreferenced()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create storage with a referenced file in it
            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            string fileContentString = "This is the file content";
            await using var fileContent = CreateStreamFromString(fileContentString);
            var fileMd5 = ComputeMd5Guid(fileContent);
            var fileId = await manager.AddFileBlobAsync(fileContent, fileContent.Length, fileMd5);
            Assert.That(fileId, Is.Not.EqualTo(Guid.Empty));

            // Serialize storage folder
            manager.PersistStorageFolderStates();

            // Create a new manager and add storage folder back (resulting in files being present in the storage folder
            // but unreferenced)
            manager = new FileBlobsManager(m_LoggerMock.Object);
            manager.AddStorageFolder(folderConfig);

            // Try to decrease its reference count
            Assert.ThrowsAsync<InvalidOperationException>(() => manager.DecreaseFileBlobReferenceAsync(fileId));
        }

        [Test]
        public async Task LockFileBlob()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content
            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);
            Assert.That(file1Id, Is.Not.EqualTo(Guid.Empty));

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);
            Assert.That(file2Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(file2Id, Is.Not.EqualTo(file1Id));

            // Increase usage count of file1 of 1 (to test that only the second decrease will block).
            manager.IncreaseFileBlobReference(file1Id);

            // Lock it
            Task decreaseTask;
            using (FileBlobLock file1Lock = await manager.LockFileBlob(file1Id))
            {
                Assert.That(file1Lock.Id, Is.EqualTo(file1Id));
                Assert.That(file1Lock.Md5, Is.EqualTo(file1Md5));
                Assert.That(file1Lock.CompressedSize, Is.EqualTo(42));
                Assert.That(file1Lock.Size, Is.EqualTo(file1Content.Length));
                Assert.That(File.Exists(file1Lock.Path), Is.True);
                // Remarks: We could be tempted to test the content of the file (which is compressed) but this is not
                // necessary since other test that deal with adding the same file again would fail if the content of
                // the file would be wrong.

                CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

                // Decrease reference count of the locked files, should work without problems (not affected by the lock
                // since the file does not have to be deleted).
                await manager.DecreaseFileBlobReferenceAsync(file1Id);

                CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 86, MaximumSize = 1000 } });

                // But decreasing it again should "stall" since the file is locked
                decreaseTask = manager.DecreaseFileBlobReferenceAsync(file1Id);
                await Task.Delay(100); // In case decreaseTask would be able to complete
                Assert.That(decreaseTask.IsCompleted, Is.False);

                // We should however be able to decrease reference count of file2 (and delete it)
                await manager.DecreaseFileBlobReferenceAsync(file2Id);
                CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 42, MaximumSize = 1000 } });
                Assert.That(decreaseTask.IsCompleted, Is.False);

                // Let's exit the scope, which will unlock the locked file and allow the decrease to finally complete.
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(decreaseTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(decreaseTask)); // Or else await timed out

            CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });
            string? filesOfStorage = Directory.GetFiles(folderConfig.Path, "*",
                    new EnumerationOptions() { RecurseSubdirectories = true }).FirstOrDefault();
            Assert.That(filesOfStorage, Is.Null);
        }

        [Test]
        public async Task RemoveStorageFolder()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "this is the file content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            // Remove a folder that does not exist
            Assert.ThrowsAsync<ArgumentException>(() => manager.RemoveStorageFolderAsync(GetNewStorageFolder()));

            // Create a tiny storage
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 50;
            manager.AddStorageFolder(folder2Config);

            // There is nothing in folder2Config, we should be able to remove it easily
            await manager.RemoveStorageFolderAsync(folder2Config.Path);

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 84, MaximumSize = 1000 } });

            // Add it back and now to the serious test, try to remove folder1 (but will fail since folder2 is not large
            // enough for both files).
            manager.AddStorageFolder(folder2Config);
            Assert.ThrowsAsync<StorageFolderFullException>(() => manager.RemoveStorageFolderAsync(folder1Config.Path));

            // Create a second tiny storage (large enough for both files with folder2)
            StorageFolderConfig folder3Config = new();
            folder3Config.Path = GetNewStorageFolder();
            folder3Config.MaximumSize = 50;
            manager.AddStorageFolder(folder3Config);

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 84, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 0,  MaximumSize = 50   },
                new StorageFolderStatus() { Path = folder3Config.Path, CurrentSize = 0,  MaximumSize = 50   } });

            await manager.RemoveStorageFolderAsync(folder1Config.Path);

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 42, MaximumSize = 50   },
                new StorageFolderStatus() { Path = folder3Config.Path, CurrentSize = 42, MaximumSize = 50   } });
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(1));
            Assert.That(CountFilesInFolder(folder3Config.Path), Is.EqualTo(1));

            await TestFileOfBlobPresent(manager, file1Id);
            await TestFileOfBlobPresent(manager, file2Id);

            // Validate there was no problem transferring the file by unreferencing them
            await manager.DecreaseFileBlobReferenceAsync(file1Id);
            await manager.DecreaseFileBlobReferenceAsync(file2Id);

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 0, MaximumSize = 50   },
                new StorageFolderStatus() { Path = folder3Config.Path, CurrentSize = 0, MaximumSize = 50   } });
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(0));
            Assert.That(CountFilesInFolder(folder3Config.Path), Is.EqualTo(0));
        }

        [Test]
        public async Task RemoveStorageFolderWithInUse()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "this is the file content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            // Create another folder in which it could be moved
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Lock one of the files
            Task removeTask;
            using (await manager.LockFileBlob(file1Id))
            {
                // Let's create a storage folder with nothing in it, should be able to remove it right after without
                // having to wait for lockedFile1.
                StorageFolderConfig folder3Config = new();
                folder3Config.Path = GetNewStorageFolder();
                folder3Config.MaximumSize = 1000;
                manager.AddStorageFolder(folder3Config);
                await manager.RemoveStorageFolderAsync(folder3Config.Path);

                // Now let's start to remove folder 1, it should get stuck until we let go of lockedFile1
                removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
                await Task.Delay(100); // In case removeTask could have the time to complete
                Assert.That(removeTask.IsCompleted, Is.False);
            }

            // We just released the lock file, removing of folder should now be able to complete.
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(removeTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask)); // Or else await timed out -> RemoveStorageFolderAsync is stuck

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 84, MaximumSize = 1000 } });

            await TestFileOfBlobPresent(manager, file1Id);
            await TestFileOfBlobPresent(manager, file2Id);
        }

        [Test]
        public async Task RemoveStorageFolderWhileAdding()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            await using var file1Content = CreateStreamFromHexString(k_CollisionFile1ContentHex);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Create another folder in which it could be moved
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Start adding a second file (with the same md5 checksum) but stall its stream
            byte[] file2Content = Convert.FromHexString(k_CollisionFile2ContentHex);
            using ClientServerStream file2Streams = new();
            int file2FirstChunkSize = file2Content.Length / 2;
            await file2Streams.WriteStream.WriteAsync(file2Content, 0, file2FirstChunkSize); // Only write the first half
            var file2Md5 = ComputeMd5Guid(file2Content);
            Assert.That(file2Md5, Is.EqualTo(file1Md5));
            var file2Task = manager.AddFileBlobAsync(file2Streams.ReadStream, file2Content.Length, file2Md5);
            await Task.Delay(100); // In case file2Task would be able to complete
            Assert.That(file2Task.IsCompleted, Is.False);

            // Start removing folder 1, should stall because file1 is locked because comparing content with the file
            // we are adding.
            var removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
            await Task.Delay(100); // In case removeTask could have the time to complete
            Assert.That(removeTask.IsCompleted, Is.False);

            // Complete production of file2.
            await file2Streams.WriteStream.WriteAsync(file2Content, file2FirstChunkSize, file2Content.Length - file2FirstChunkSize);
            file2Streams.WriteStream.Close();

            // Everything should now complete
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(file2Task, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(file2Task)); // Or else await timed out -> AddFileBlobAsync is stuck
            Guid file2Id = file2Task.Result;
            awaitTask = await Task.WhenAny(removeTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask)); // Or else await timed out -> RemoveStorageFolderAsync is stuck

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 302, MaximumSize = 1000 } });

            await TestFileOfBlobPresent(manager, file1Id);
            await TestFileOfBlobPresent(manager, file2Id);
        }

        [Test]
        public async Task AddFileInFolderBeingRemoved()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Create a second folder.
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Add some bigger content in it so that it is not chosen while adding the file we will add during removal.
            byte[] file2ContentBytes = GetRandomBytes(256);
            await using var file2Content = CreateStreamFromBytes(file2ContentBytes);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            Assert.That(CountFilesInFolder(folder1Config.Path), Is.EqualTo(1));
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(1));

            // Lock the file so that remove of the folder cannot complete instantaneously
            Task removeTask;
            Guid file3Id;
            using (await manager.LockFileBlob(file1Id))
            {
                // Start the remove
                removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
                await Task.Delay(100); // In case removeTask would be able to complete
                Assert.That(removeTask.IsCompleted, Is.False);

                // Add a new file, should be added to the folder being removed since it has more free capacity
                string file3ContentString = "this is the file content";
                await using var file3Content = CreateStreamFromString(file3ContentString);
                var file3Md5 = ComputeMd5Guid(file3Content);
                file3Id = await manager.AddFileBlobAsync(file3Content, file3Content.Length, file3Md5);

                Assert.That(CountFilesInFolder(folder1Config.Path), Is.EqualTo(2));
            }

            // We just released the lock file, removing of folder should now be able to complete.
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(removeTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask)); // Or else await timed out -> RemoveStorageFolderAsync is stuck

            await TestFileOfBlobPresent(manager, file1Id);
            await TestFileOfBlobPresent(manager, file2Id);
            await TestFileOfBlobPresent(manager, file3Id);
        }

        [Test]
        public async Task RemoveStorageFolderConcurrent()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Create some content in folder 2
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            string file2ContentString = "this is the file content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            // Create folder 3 that will receive everything once we removed folder 1 and folder 2.
            StorageFolderConfig folder3Config = new();
            folder3Config.Path = GetNewStorageFolder();
            folder3Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder3Config);

            // Lock the file so that remove of the folder cannot complete instantaneously
            Task removeTask1;
            Task removeTask2;
            using (await manager.LockFileBlob(file1Id))
            {
                // Remove the folder containing file1Id.
                removeTask1 = manager.RemoveStorageFolderAsync(folder1Config.Path);

                // Start a remove of the folder containing file2Id.  By itself it should work, however it will also have
                // to wait because waiting on removeTask1 to complete before starting (only one RemoveStorageFolder can
                // work simultaneously).
                removeTask2 = manager.RemoveStorageFolderAsync(folder2Config.Path);

                await Task.Delay(100); // In case removeTask would be able to complete
                Assert.That(removeTask1.IsCompleted, Is.False);
                Assert.That(removeTask2.IsCompleted, Is.False);
            }

            // We just released the lock file, removing of folder should now be able to complete.
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(removeTask1, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask1)); // Or else await timed out
            awaitTask = await Task.WhenAny(removeTask2, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask2)); // Or else await timed out

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder3Config.Path, CurrentSize = 84, MaximumSize = 1000 } });

            await TestFileOfBlobPresent(manager, file1Id);
            await TestFileOfBlobPresent(manager, file2Id);
        }

        [Test]
        public async Task RemoveStorageFolderOfLockedFileBeingRemoved()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "this is the file content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Lock a file
            Task removeTask;
            Task decreaseReferenceTask;
            using (await manager.LockFileBlob(file1Id))
            {
                // Remove the folder containing that locked file
                removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);

                // The remove waits because the file is locked

                // Make it even harder by trying to remove the reference to that locked file
                decreaseReferenceTask = manager.DecreaseFileBlobReferenceAsync(file1Id);

                // Both of those should be waiting for the file to get unlocked
                await Task.Delay(100); // In case removeTask would be able to complete
                Assert.That(removeTask.IsCompleted, Is.False);
                Assert.That(decreaseReferenceTask.IsCompleted, Is.False);

                // Wait to be sure everything had the time to happen:
                // file1 get copied to folder 2 (but waiting to get deleted)
                // file2 get copied
                while (CountFilesInFolder(folder2Config.Path) != 2)
                {
                    await Task.Delay(50);
                }
                Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(2));
            }

            // We just released the lock file, removing of folder should now be able to complete.
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(removeTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(removeTask)); // Or else await timed out
            awaitTask = await Task.WhenAny(decreaseReferenceTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(decreaseReferenceTask)); // Or else await timed out

            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 42, MaximumSize = 1000 } });

            Assert.ThrowsAsync<KeyNotFoundException>(() => manager.LockFileBlob(file1Id));
            await TestFileOfBlobPresent(manager, file2Id);
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(1));
        }

        [Test]
        public async Task RemoveStorageFolderWithFileBeingAdded()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            byte[] file1ContentBytes = GetRandomBytes(256);
            await using var file1Content = CreateStreamFromBytes(file1ContentBytes);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Start adding file 2 but let's not complete it yet
            byte[] file2ContentBytes = GetRandomBytes(256);
            using ClientServerStream file2Streams = new();
            int file2FirstChunkSize = file2ContentBytes.Length / 2;
            await file2Streams.WriteStream.WriteAsync(file2ContentBytes, 0, file2FirstChunkSize); // Only write the first half
            var file2Md5 = ComputeMd5Guid(file2ContentBytes);
            var file2Task = manager.AddFileBlobAsync(file2Streams.ReadStream, file2ContentBytes.Length, file2Md5);

            // Create a folder 2 (with nothing in it yet) to which all the content will be moved when we remove folder 1
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            var removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
            await Task.Delay(100); // In case removeTask would be able to complete
            Assert.That(removeTask.IsCompleted, Is.False);

            // The file that was completed should be moved over ASAP (not wait for the non ready file to complete).
            while (CountFilesInFolder(folder2Config.Path) != 1)
            {
                await Task.Delay(50);
            }
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(1)); // file2 that is still being created
            Assert.That(CountFilesInFolder(folder1Config.Path), Is.EqualTo(1)); // file1 that has been moved

            // Conclude file 2
            await file2Streams.WriteStream.WriteAsync(file2ContentBytes, file2FirstChunkSize, file2ContentBytes.Length - file2FirstChunkSize);
            file2Streams.WriteStream.Close();
            var file2Id = await file2Task;

            // And removing storage should complete
            await removeTask;
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(2));

            await TestFileOfBlobContent(manager, file1Id, file1ContentBytes);
            await TestFileOfBlobContent(manager, file2Id, file2ContentBytes);
        }

        [Test]
        public async Task RemoveStorageFolderRunsOutOfSpace()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 300;
            manager.AddStorageFolder(folder1Config);

            // Add file 1
            byte[] file1ContentBytes = GetRandomBytes(16);
            await using var file1Content = CreateStreamFromBytes(file1ContentBytes);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Then file 2 but let's not complete it yet
            byte[] file2ContentBytes = GetRandomBytes(256);
            using ClientServerStream file2Streams = new();
            int file2FirstChunkSize = file2ContentBytes.Length / 2;
            await file2Streams.WriteStream.WriteAsync(file2ContentBytes, 0, file2FirstChunkSize); // Only write the first half
            var file2Md5 = ComputeMd5Guid(file2ContentBytes);
            var file2Task = manager.AddFileBlobAsync(file2Streams.ReadStream, file2ContentBytes.Length, file2Md5);

            // Create a second folder to receive the moved content
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 300;
            manager.AddStorageFolder(folder2Config);

            // Start removing folder 1 that cannot complete because it contains a file is still being produced
            var removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
            await Task.Delay(100); // In case removeTask would be able to complete
            Assert.That(removeTask.IsCompleted, Is.False);

            // Removing folder should move all the files it can and then wait after the blocking files.  So file 1
            // should be moved and the process will then wait for file 2.
            while (CountFilesInFolder(folder1Config.Path) != 1)
            {
                await Task.Delay(50);
            }
            Assert.That(CountFilesInFolder(folder1Config.Path), Is.EqualTo(1));
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(1));

            // Add a new file.  Since folder 2 only contains a 16 bytes file, it should go in folder 2.
            byte[] file3ContentBytes = GetRandomBytes(256);
            await using var file3Content = CreateStreamFromBytes(file3ContentBytes);
            var file3Md5 = ComputeMd5Guid(file3Content);
            var file3Id = await manager.AddFileBlobAsync(file3Content, file3Content.Length, file3Md5);

            Assert.That(CountFilesInFolder(folder1Config.Path), Is.EqualTo(1));
            Assert.That(CountFilesInFolder(folder2Config.Path), Is.EqualTo(2));

            // Conclude writing file 1
            await file2Streams.WriteStream.WriteAsync(file2ContentBytes, file2FirstChunkSize, file2ContentBytes.Length - file2FirstChunkSize);
            file2Streams.WriteStream.Close();
            var file2Id = await file2Task;

            // Wait for remove storage folder to conclude -> throwing an exception
            Assert.ThrowsAsync<StorageFolderFullException>(() => removeTask);

            // Verify all the content is still intact
            using (var fileLock = await manager.LockFileBlob(file1Id))
                Assert.That(fileLock.Path.StartsWith(folder2Config.Path), Is.True);
            using (var fileLock = await manager.LockFileBlob(file2Id))
                Assert.That(fileLock.Path.StartsWith(folder1Config.Path), Is.True);
            using (var fileLock = await manager.LockFileBlob(file3Id))
                Assert.That(fileLock.Path.StartsWith(folder2Config.Path), Is.True);

            await TestFileOfBlobContent(manager, file1Id, file1ContentBytes);
            await TestFileOfBlobContent(manager, file2Id, file2ContentBytes);
            await TestFileOfBlobContent(manager, file3Id, file3ContentBytes);
        }

        [Test]
        public async Task RemoveStorageFolderCopyFails()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            // Create folder 2 (where files should move when folder 1 is removed)
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // However, we want to copy to folder 2 operation to fail, so we will manually create a read only file with
            // the same name the moved file 1 should have.
            string folder2BlobPath = GetBlobPath(folder2Config.Path, file1Id);
            Directory.CreateDirectory(Path.GetDirectoryName(folder2BlobPath)!);
            await File.WriteAllTextAsync(folder2BlobPath, "Some bad content");
            File.SetAttributes(folder2BlobPath, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(folder2BlobPath);

            // Try to remove the storage
            Assert.ThrowsAsync<IOException>(() => manager.RemoveStorageFolderAsync(folder1Config.Path));

            // The read only file we create to cause the failure will be considered as a zombie
            m_LoggerMock.VerifyLog(l => l.LogError("*will be added to zombies that we will try to remove on next restart*"));
        }

        [Test]
        public async Task RemoveStorageExceptionWithPendingMove()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Create some content in folder 1
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            // Create a folder 2 that is to receive the moved content
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Create a read only file in folder 2 that will cause an error when trying to move
            string folder2BlobPath = GetBlobPath(folder2Config.Path, file2Id);
            Directory.CreateDirectory(Path.GetDirectoryName(folder2BlobPath)!);
            await File.WriteAllTextAsync(folder2BlobPath, "Some bad content");
            File.SetAttributes(folder2BlobPath, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(folder2BlobPath);

            // Lock file 1
            using (await manager.LockFileBlob(file1Id))
            {
                // Remove the storage folder which triggers moving both files to folder 2.
                // File1 will be blocked (pending move) and file 2 throw.  So depending on which one is processed first
                // we might not test what we wanted to test, but still the test should pass.
                var removeTask = manager.RemoveStorageFolderAsync(folder1Config.Path);
                Assert.ThrowsAsync<IOException>(() => removeTask);
                m_LoggerMock.VerifyLog(l => l.LogError("*will be added to zombies that we will try to remove on next restart*"));
            }

            // Both files should still be in folder 1, even file 1 since the call to RemoveStorageFolderAsync failed
            // before we can conclude the file move.
            using (var fileLock = await manager.LockFileBlob(file1Id))
                Assert.That(fileLock.Path.StartsWith(folder1Config.Path), Is.True);
            using (var fileLock = await manager.LockFileBlob(file2Id))
                Assert.That(fileLock.Path.StartsWith(folder1Config.Path), Is.True);
        }

        [Test]
        public async Task MakeStorageFolderSmaller()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Initial create of the storage folder
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            // Add some content
            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "this is the file Content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 84, MaximumSize = 1000 } });

            // Smaller but still just big enough
            folder1Config.MaximumSize = 84;
            await manager.UpdateStorageFolderAsync(folder1Config);

            // 1 byte too small and no where for the files to go
            folder1Config.MaximumSize = 83;
            Assert.ThrowsAsync<StorageFolderFullException>(() => manager.UpdateStorageFolderAsync(folder1Config));

            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 41;
            manager.AddStorageFolder(folder2Config);

            // Now there is a folder but not enough capacity
            Assert.ThrowsAsync<StorageFolderFullException>(() => manager.UpdateStorageFolderAsync(folder1Config));

            // Make the folder large enough
            folder2Config.MaximumSize = 42;
            await manager.UpdateStorageFolderAsync(folder2Config);
            await manager.UpdateStorageFolderAsync(folder1Config);

            // Move everything to the second folder (by making the first one really small)
            folder2Config.MaximumSize = 84;
            await manager.UpdateStorageFolderAsync(folder2Config);
            folder1Config.MaximumSize = 0;
            await manager.UpdateStorageFolderAsync(folder1Config);

            // Validate status of folders
            CompareStatus(manager, new[] {
                new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 0, MaximumSize = 0 },
                new StorageFolderStatus() { Path = folder2Config.Path, CurrentSize = 84, MaximumSize = 84 } });
        }

        [Test]
        public async Task SqueezeFolderFullOfZombies()
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Initial create of the storage folder
            StorageFolderConfig folder1Config = new();
            folder1Config.Path = GetNewStorageFolder();
            folder1Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder1Config);

            // Add some content
            string file1ContentString = "This is the file content";
            await using var file1Content = CreateStreamFromString(file1ContentString);
            var file1Md5 = ComputeMd5Guid(file1Content);
            var file1Id = await manager.AddFileBlobAsync(file1Content, file1Content.Length, file1Md5);

            string file2ContentString = "this is the file Content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            var file2Id = await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 84, MaximumSize = 1000 } });

            // Transform content into zombies
            string file1BlobPath = GetBlobPath(folder1Config.Path, file1Id);
            File.SetAttributes(file1BlobPath, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(file1BlobPath);
            string file2BlobPath = GetBlobPath(folder1Config.Path, file2Id);
            File.SetAttributes(file2BlobPath, FileAttributes.ReadOnly);
            m_FileToClearAttributes.Add(file2BlobPath);

            await manager.DecreaseFileBlobReferenceAsync(file1Id);
            await manager.DecreaseFileBlobReferenceAsync(file2Id);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folder1Config.Path, CurrentSize = 84, ZombiesSize = 84,
                                                    MaximumSize = 1000 } });

            // Create a new storage folder that could receive content from folder 1
            StorageFolderConfig folder2Config = new();
            folder2Config.Path = GetNewStorageFolder();
            folder2Config.MaximumSize = 1000;
            manager.AddStorageFolder(folder2Config);

            // Resize the folder just large enough to accommodate zombies shouldn't cause problems.
            folder1Config.MaximumSize = 84;
            await manager.UpdateStorageFolderAsync(folder1Config);

            // However anything small would result in zombie juice (exception)
            folder1Config.MaximumSize = 83;
            Assert.ThrowsAsync<InvalidOperationException>(() => manager.UpdateStorageFolderAsync(folder1Config));
            m_LoggerMock.VerifyLog(l => l.LogError("*will be added to zombies that we will try to remove on next restart*"));
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public async Task DoesNotLoadBackNotReadyFiles(bool completeFile)
        {
            var manager = new FileBlobsManager(m_LoggerMock.Object);

            // Initial create of the storage folder
            StorageFolderConfig folderConfig = new();
            folderConfig.Path = GetNewStorageFolder();
            folderConfig.MaximumSize = 1000;
            manager.AddStorageFolder(folderConfig);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 0, MaximumSize = 1000 } });

            // Add some content
            byte[] file1Content = Convert.FromHexString(k_CollisionFile1ContentHex);
            using ClientServerStream file1Streams = new();
            int file1FirstChunkSize = file1Content.Length / 2;
            await file1Streams.WriteStream.WriteAsync(file1Content, 0, file1FirstChunkSize); // Only write the first half
            var file1Md5 = ComputeMd5Guid(file1Content);
            var longAddFileBlobTask = manager.AddFileBlobAsync(file1Streams.ReadStream, file1Content.Length, file1Md5);

            string file2ContentString = "This is a different content";
            await using var file2Content = CreateStreamFromString(file2ContentString);
            var file2Md5 = ComputeMd5Guid(file2Content);
            await manager.AddFileBlobAsync(file2Content, file2Content.Length, file2Md5);

            CompareStatus(manager,
                new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 172, MaximumSize = 1000 } });

            // Serialize storage folder (with a non ready file)
            manager.PersistStorageFolderStates();

            if (completeFile)
            {
                await file1Streams.WriteStream.WriteAsync(file1Content, file1FirstChunkSize,
                    file1Content.Length - file1FirstChunkSize);
            }
            file1Streams.WriteStream.Close();

            // We need to wait for the file to be completely added or otherwise the new FileBlobsManager we create a few
            // lines below could try to delete a file that is still in use by the previous manager that is completing
            // its add.
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var awaitTask = await Task.WhenAny(longAddFileBlobTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(longAddFileBlobTask)); // Or else await timed out -> AddFileBlobAsync is stuck

            // Create a new manager and add storage folder back
            manager = new FileBlobsManager(m_LoggerMock.Object);
            manager.AddStorageFolder(folderConfig);

            if (completeFile)
            {
                // Not ready as persist files should have been completed before loading, so file should have been kept
                m_LoggerMock.VerifyLog(l => l.LogWarning("*was tagged as not ready but it looks complete*"));
                CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 172, MaximumSize = 1000 } });
            }
            else
            {
                // File has never been completed...
                m_LoggerMock.VerifyLog(l => l.LogError("*was tagger as not ready and does not look like it is*"));
                CompareStatus(manager,
                    new[] { new StorageFolderStatus() { Path = folderConfig.Path, CurrentSize = 44, MaximumSize = 1000 } });
            }
        }

        static void CompareStatus(FileBlobsManager manager, StorageFolderStatus[] expectedStatuses)
        {
            var currentStatuses = manager.GetStorageFolderStatus();

            foreach (var expected in expectedStatuses)
            {
                var current = currentStatuses.FirstOrDefault(s => s.Path == expected.Path);
                Assert.That(current, Is.Not.Null);
                Assert.That(current!.CurrentSize, Is.EqualTo(expected.CurrentSize));
                Assert.That(current.ZombiesSize, Is.EqualTo(expected.ZombiesSize));
                Assert.That(current.MaximumSize, Is.EqualTo(expected.MaximumSize));
                current.Path = "";
            }

            foreach (var current in currentStatuses)
            {
                Assert.That(current.Path, Is.EqualTo(""));
            }
        }

        static Stream CreateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        static Stream CreateStreamFromHexString(string s)
        {
            return CreateStreamFromBytes(Convert.FromHexString(s));
        }

        static Stream CreateStreamFromBytes(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            stream.Position = 0;
            return stream;
        }

        static string GetBlobPath(string storageFolder, Guid id)
        {
            var filenameAsString = id.ToString();
            return Path.Combine(storageFolder, filenameAsString.Substring(0, 2), filenameAsString.Substring(2, 2),
                filenameAsString);
        }

        static void WriteCompressedContent(string filePath, string content)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            using var writeCompressedStream = File.OpenWrite(filePath);
            writeCompressedStream.SetLength(0);
            using GZipStream compressor = new(writeCompressedStream, CompressionMode.Compress);
            using StreamWriter streamWriter = new(compressor);
            streamWriter.Write(content);
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "FileBlobsManagerTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        static void CloneDirectory(string from, string to)
        {
            foreach (var directory in Directory.GetDirectories(from))
            {
                string dirName = Path.GetFileName(directory);
                if (!Directory.Exists(Path.Combine(to, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(to, dirName));
                }
                CloneDirectory(directory, Path.Combine(to, dirName));
            }

            foreach (var file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }
        }

        static async Task TestFileOfBlobPresent(FileBlobsManager manager, Guid fileBlobId)
        {
            using var lockedFile = await manager.LockFileBlob(fileBlobId);
            Assert.That(File.Exists(lockedFile.Path), Is.True);
        }

        static async Task TestFileOfBlobContent(FileBlobsManager manager, Guid fileBlobId, byte[] expectedContent)
        {
            using var lockedFile = await manager.LockFileBlob(fileBlobId);
            Assert.That(File.Exists(lockedFile.Path), Is.True);

            await using var compressedStream = File.OpenRead(lockedFile.Path);
            await using GZipStream decompressedStream = new(compressedStream, CompressionMode.Decompress);
            byte[] decompressedBytes = new byte[expectedContent.Length];
            int decompressedLength = await decompressedStream.ReadAsync(decompressedBytes);
            byte[] anotherSmallBuffer = new byte[1024];
            Assert.That(await decompressedStream.ReadAsync(anotherSmallBuffer), Is.EqualTo(0));

            Assert.That(decompressedLength, Is.EqualTo(expectedContent.Length));
            Assert.That(decompressedBytes, Is.EqualTo(expectedContent));
        }

        static int CountFilesInFolder(string folder)
        {
            return Directory.GetFiles(folder, "*",
                new EnumerationOptions() { RecurseSubdirectories = true }).Count();
        }

        List<string> m_StorageFolders = new();
        List<string> m_FileToClearAttributes = new();
        Mock<ILogger> m_LoggerMock = new();

        // Clashing md5 contents from https://www.mscs.dal.ca/~selinger/md5collision/
        const string k_CollisionFile1ContentHex = "d131dd02c5e6eec4693d9a0698aff95c2fcab58712467eab4004583eb8fb7f8955" +
            "ad340609f4b30283e488832571415a085125e8f7cdc99fd91dbdf280373c5bd8823e3156348f5bae6dacd436c919c6dd53e2b487" +
            "da03fd02396306d248cda0e99f33420f577ee8ce54b67080a80d1ec69821bcb6a8839396f9652b6ff72a70";
        const string k_CollisionFile2ContentHex = "d131dd02c5e6eec4693d9a0698aff95c2fcab50712467eab4004583eb8fb7f8955" +
            "ad340609f4b30283e4888325f1415a085125e8f7cdc99fd91dbd7280373c5bd8823e3156348f5bae6dacd436c919c6dd53e23487" +
            "da03fd02396306d248cda0e99f33420f577ee8ce54b67080280d1ec69821bcb6a8839396f965ab6ff72a70";
    }
}
