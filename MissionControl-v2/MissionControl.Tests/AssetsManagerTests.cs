using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class AssetsManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
            m_FileBlobsManagerMock = new(MockBehavior.Strict, m_LoggerMock.Object);
            m_PayloadsManagerMock = new(MockBehavior.Strict, m_LoggerMock.Object, GetNewStorageFolder(),
                m_FileBlobsManagerMock.Object);
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
        public async Task AddRemoveAsset()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);
            Stream file3Stream = CreateStreamFromString("This is the third file");
            string file3Md5 = ComputeMd5String(file3Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                        }
                    },
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload2",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "fileA", Md5 = file2Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "fileB", Md5 = file3Md5 },
                        }
                    },
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1", "Payload2" }
                    }
                }
            };

            // Prepare all the calls to the mocks adding an asset should result in
            MockSequence theSequence = new();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(theSequence).Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file2"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            var fileBlob2Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("fileA"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence).Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("fileB"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file3Stream), file3Stream.Length));
            var fileBlob3Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file3Stream, file3Stream.Length, Md5AsGuid(file3Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob3Guid);
            m_FileBlobsManagerMock.InSequence(theSequence).Setup(fbm => fbm.LockFileBlob(fileBlob3Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob3Guid, Md5AsGuid(file3Md5), 24, file3Stream.Length));

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);
            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);

            // Remarks: Might look strange to have a DecreaseFileBlobReferenceAsync even in the case of a success but
            // this is ok as normal implementation of AddPayloadAsync should have increased the reference of file blobs
            // it uses.
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob3Guid)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid)).Returns(Task.CompletedTask);

            // Add the asset
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Guid assetId = await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object);

            List<Guid> assetPayloads = new();
            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value.Count, Is.EqualTo(1));
                var asset = lockedCollection.Value.Values.First();
                Assert.That(asset.Id, Is.EqualTo(assetId));
                Assert.That(asset.Name, Is.EqualTo("TestName"));
                Assert.That(asset.Description, Is.EqualTo("TestDescription"));
                Assert.That(asset.Launchables.Count(), Is.EqualTo(1));
                var launchable = asset.Launchables.First();
                Assert.That(launchable.Name, Is.EqualTo("Cluster Node"));
                Assert.That(launchable.Type, Is.EqualTo("clusterNode"));
                Assert.That(launchable.Payloads.Count, Is.EqualTo(2));
                assetPayloads.AddRange(launchable.Payloads);
                Assert.That(asset.StorageSize, Is.EqualTo(94));
            }

            // Prepare all the calls to the mocks removing the asset should result in.  In fact, this isn't much, we do
            // not get called to "undo" the adding of files as this is normally done as a consequence of calling
            // PayloadsManager.RemovePayloadAsync (and we are mocking it).
            foreach (var payloadId in assetPayloads)
            {
                m_PayloadsManagerMock.InSequence(theSequence).Setup(pm => pm.RemovePayloadAsync(payloadId))
                    .Returns(Task.CompletedTask);
            }

            // Do the remove
            bool removeRet = await assetsManager.RemoveAssetAsync(Guid.NewGuid());
            Assert.That(removeRet, Is.False);
            removeRet = await assetsManager.RemoveAssetAsync(assetId);
            Assert.That(removeRet, Is.True);
            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            // Ensure all the calls in the sequence have been performed
            bool theSequenceCompleted = false;
            m_PayloadsManagerMock.InSequence(theSequence).Setup(pm => pm.RemovePayloadAsync(Guid.Empty))
                .Returns(Task.CompletedTask)
                .Callback(() => theSequenceCompleted = true);
            await m_PayloadsManagerMock.Object.RemovePayloadAsync(Guid.Empty);
            Assert.That(theSequenceCompleted, Is.True);
        }

        [Test]
        public async Task ErrorAddingFileBlob()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1" }
                    }
                }
            };

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            MockSequence assetSourceSequence = new();
            bool assetSourceSequenceEndReached = false;
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog);
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetFileContentAsync("file2"))
                .Callback(() => assetSourceSequenceEndReached = true)
                .Throws<InvalidOperationException>();

            var fileBlob1Guid = Guid.NewGuid();
            MockSequence filesBlobManagerSequence = new();
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));
            bool filesBlobManagerSequenceCompleted = false;
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid))
                .Returns(Task.CompletedTask)
                .Callback(() => filesBlobManagerSequenceCompleted = true);

            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.ThrowsAsync<InvalidOperationException>(
                () => assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object));

            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            Assert.That(assetSourceSequenceEndReached, Is.True);
            Assert.That(filesBlobManagerSequenceCompleted, Is.True);
        }

        [Test]
        public async Task ErrorAddingPayload()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);
            Stream file3Stream = CreateStreamFromString("This is the third file");
            string file3Md5 = ComputeMd5String(file3Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                        }
                    },
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload2",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "fileA", Md5 = file2Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "fileB", Md5 = file3Md5 },
                        }
                    },
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1", "Payload2" }
                    }
                }
            };

            MockSequence theSequence = new();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog);

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file2"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            var fileBlob2Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("fileA"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("fileB"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file3Stream), file3Stream.Length));
            var fileBlob3Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file3Stream, file3Stream.Length, Md5AsGuid(file3Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob3Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob3Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob3Guid, Md5AsGuid(file3Md5), 24, file3Stream.Length));

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);
            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Throws<ArgumentException>();

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.RemovePayloadAsync(It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob3Guid))
                .Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid))
                .Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid))
                .Returns(Task.CompletedTask);
            bool theSequenceCompleted = false;
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid))
                .Returns(Task.CompletedTask)
                .Callback(() => theSequenceCompleted = true);

            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.ThrowsAsync<ArgumentException>(
                () => assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object));

            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            Assert.That(theSequenceCompleted, Is.True);
        }

        [Test]
        public async Task CancelFileBlobsLongToAdd()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);
            Stream file3Stream = CreateStreamFromString("This is the third file");
            string file3Md5 = ComputeMd5String(file3Stream);
            Stream file4Stream = CreateStreamFromString("This is the fourth file");
            string file4Md5 = ComputeMd5String(file4Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file3", Md5 = file3Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file4", Md5 = file4Md5 },
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1" }
                    }
                }
            };

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            MockSequence assetSourceSequence = new();
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog);
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetFileContentAsync("file2"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            bool assetSourceSequenceEndReached = false;
            assetSourceMock.InSequence(assetSourceSequence).Setup(a => a.GetFileContentAsync("file3"))
                .Callback(() => assetSourceSequenceEndReached = true)
                .Throws<InvalidOperationException>();
            // We don't add file4, it should never be asked since file3 will throw

            MockSequence filesBlobManagerSequence = new();
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            // Setup AddFileBlobAsync for file2 to block until canceled (and throw)
            CancellationToken addAssetAsyncCancellationToken = new();
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .Callback<Stream, long, Guid, CancellationToken>((_, _, _, cancellationToken) => { addAssetAsyncCancellationToken = cancellationToken; })
                .Returns(async () => {
                    for (; ; )
                    {
                        await Task.Delay(10, addAssetAsyncCancellationToken);
                    }
                    // ReSharper disable once FunctionNeverReturns
                });
            // Only call AddFileBlobAsync for file2, all the following steps are not executed (not
            // DecreaseFileBlobReferenceAsync since AddFileBlobAsync never completes).
            // Nothing gets called for file3 since GetFileContentAsync fails
            // Nothing gets called for file4 since GetFileContentAsync is not even called on it.

            // And at last we rollback file1
            bool filesBlobManagerSequenceEndReached = false;
            m_FileBlobsManagerMock.InSequence(filesBlobManagerSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid))
                .Returns(Task.CompletedTask)
                .Callback(() => filesBlobManagerSequenceEndReached = true);

            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.ThrowsAsync<InvalidOperationException>(
                () => assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object));
            Assert.That(assetSourceSequenceEndReached, Is.True);
            Assert.That(filesBlobManagerSequenceEndReached, Is.True);

            using var lockedCollection = await assetsManager.GetLockedReadOnlyAsync();
            Assert.That(lockedCollection.Value, Is.Empty);
        }

        [Test]
        public async Task ErrorReferencingMissingPayload()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1", "Missing" }
                    }
                }
            };

            MockSequence theSequence = new();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog);

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file2"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            var fileBlob2Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.RemovePayloadAsync(It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid))
                .Returns(Task.CompletedTask);
            bool theSequenceCompleted = false;
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid))
                .Returns(Task.CompletedTask)
                .Callback(() => theSequenceCompleted = true);

            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.ThrowsAsync<CatalogException>(
                () => assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object));

            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            Assert.That(theSequenceCompleted, Is.True);
        }

        [Test]
        public async Task LaunchableNameProblems()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            CreateStreamFromString("This is the second file");

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload1",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 }
                        }
                    },
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload2",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file1Md5 }
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload1" }
                    },
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload2" }
                    }
                }
            };

            MockSequence theSequence = new();
            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);

            // Test with duplicate names
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog);

            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo("Some launchable share the same name, every " +
                "launchable must have a unique name within the LaunchCatalog."));

            // Test with an empty name
            launchCatalog.Launchables.ElementAt(1).Name = "";

            bool theSequenceCompleted = false;
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync())
                .ReturnsAsync(launchCatalog)
                .Callback(() => theSequenceCompleted = true);

            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo("Launchable name cannot be empty."));

            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            Assert.That(theSequenceCompleted);
        }

        static IEnumerable<LaunchParameter> GetLaunchParameters(LaunchCatalog.Launchable from, string propertyName)
        {
            var propertyInfo = from.GetType().GetProperty(propertyName);
            Assert.That(propertyInfo, Is.Not.Null);
            var ret = propertyInfo!.GetValue(from);
            Assert.That(ret, Is.Not.Null);
            return (IEnumerable<LaunchParameter>)ret!;
        }

        static void SetLaunchParameters(LaunchCatalog.Launchable of, string propertyName, IEnumerable<LaunchParameter> value)
        {
            var propertyInfo = of.GetType().GetProperty(propertyName);
            Assert.That(propertyInfo, Is.Not.Null);
            propertyInfo!.SetValue(of, value);
        }

        [Test]
        [TestCase(nameof(Launchable.GlobalParameters))]
        [TestCase(nameof(Launchable.LaunchComplexParameters))]
        [TestCase(nameof(Launchable.LaunchPadParameters))]
        public void LaunchableMissingParameterDefaultValue(string propertyName)
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Catalog launchCatalog = new()
            {
                Launchables = new[] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode"
                    }
                }
            };
            var launchable = launchCatalog.Launchables.First();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            SetLaunchParameters(launchable, propertyName, new[] {
                new LaunchParameter() { Id = "MyId" }
            });
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo($"Parameter MyId does not have a default value."));
        }

        [Test]
        [TestCase(nameof(Launchable.GlobalParameters))]
        [TestCase(nameof(Launchable.LaunchComplexParameters))]
        [TestCase(nameof(Launchable.LaunchPadParameters))]
        public void LaunchableIntraDuplicateParameterId(string propertyName)
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Catalog launchCatalog = new()
            {
                Launchables = new[] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode"
                    }
                }
            };
            var launchable = launchCatalog.Launchables.First();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            SetLaunchParameters(launchable, propertyName, new[] {
                new LaunchParameter() { Id = "Id0", DefaultValue = 0 }, new LaunchParameter() { Id = "Id0", DefaultValue = 0 }
            });
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo($"Multiple parameters of {launchable.Name} " +
                    $"have the identifier Id0."));
        }

        [Test]
        [TestCase(nameof(Launchable.GlobalParameters), nameof(Launchable.LaunchComplexParameters))]
        [TestCase(nameof(Launchable.GlobalParameters), nameof(Launchable.LaunchPadParameters))]
        [TestCase(nameof(Launchable.LaunchComplexParameters), nameof(Launchable.LaunchPadParameters))]
        public void LaunchableInterDuplicateParameterId(string property1Name, string property2Name)
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Catalog launchCatalog = new()
            {
                Launchables = new[] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode",
                        GlobalParameters = new[] { new LaunchParameter() {Id = "GlobalId", DefaultValue = 42} },
                        LaunchComplexParameters = new[] { new LaunchParameter() {Id = "LaunchComplexId", DefaultValue = 28} },
                        LaunchPadParameters = new[] { new LaunchParameter() {Id = "LaunchPadId", DefaultValue = "Something"} }
                    }
                }
            };
            var launchable = launchCatalog.Launchables.First();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            SetLaunchParameters(launchable, property1Name,
                GetLaunchParameters(launchable, property1Name).Append(new LaunchParameter() { Id = "Duplicate", DefaultValue = 100}));
            SetLaunchParameters(launchable, property2Name,
                GetLaunchParameters(launchable, property2Name).Append(new LaunchParameter() { Id = "Duplicate", DefaultValue = 200}));
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo($"Multiple parameters of {launchable.Name} " +
                    $"have the identifier Duplicate."));
        }

        [Test]
        [TestCase(nameof(Launchable.GlobalParameters))]
        [TestCase(nameof(Launchable.LaunchComplexParameters))]
        [TestCase(nameof(Launchable.LaunchPadParameters))]
        public void LaunchableParameterDefaultValueDoesNotRespectConstraints(string propertyName)
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Catalog launchCatalog = new()
            {
                Launchables = new[] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Launchable",
                        Type = "clusterNode"
                    }
                }
            };
            var launchable = launchCatalog.Launchables.First();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            SetLaunchParameters(launchable, propertyName, new[] {
                new LaunchParameter() { Id = "MyId", DefaultValue = "Missing",
                    Constraint = new ListConstraint() { Choices = new [] { "Not", "In", "List" } } }
            });
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            Assert.That(async () => { await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object); },
                Throws.TypeOf<CatalogException>().With.Message.EqualTo($"Parameter MyId default value Missing does " +
                    $"respect the parameter's constraints."));
        }

        [Test]
        public async Task SaveLoad()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);
            Stream file2Stream = CreateStreamFromString("This is the second file");
            string file2Md5 = ComputeMd5String(file2Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 },
                            new LaunchCatalog.PayloadFile() { Path = "file2", Md5 = file2Md5 },
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload" }
                    }
                }
            };

            // Prepare all the calls to the mocks adding an asset should result in
            MockSequence theSequence = new();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(theSequence).Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file2"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file2Stream), file2Stream.Length));
            var fileBlob2Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file2Stream, file2Stream.Length, Md5AsGuid(file2Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob2Guid);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.LockFileBlob(fileBlob2Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob2Guid, Md5AsGuid(file2Md5), 28, file2Stream.Length));

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);

            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob2Guid)).Returns(Task.CompletedTask);
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid)).Returns(Task.CompletedTask);

            // Add the asset
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object);

            // Save it
            MemoryStream saveStream = new();
            await assetsManager.SaveAsync(saveStream);

            // Try to load in the "old manager" (that already contains something)
            saveStream.Position = 0;
            Assert.Throws<InvalidOperationException>(() => assetsManager.Load(saveStream));

            // Load in a brand new squeaky clean manager
            assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);
            saveStream.Position = 0;
            assetsManager.Load(saveStream);

            // Check everything seem to be loaded back correctly.
            Guid assetId;
            List<Guid> assetPayloads = new();
            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value.Count, Is.EqualTo(1));
                var asset = lockedCollection.Value.Values.First();
                assetId = asset.Id;
                Assert.That(asset.Name, Is.EqualTo("TestName"));
                Assert.That(asset.Description, Is.EqualTo("TestDescription"));
                Assert.That(asset.Launchables.Count(), Is.EqualTo(1));
                var launchable = asset.Launchables.First();
                Assert.That(launchable.Name, Is.EqualTo("Cluster Node"));
                Assert.That(launchable.Type, Is.EqualTo("clusterNode"));
                Assert.That(launchable.Payloads.Count, Is.EqualTo(1));
                assetPayloads.AddRange(launchable.Payloads);
                Assert.That(asset.StorageSize, Is.EqualTo(70));
            }

            // I know remove is already tested somewhere else, but this is a quick test that everything seem to be in
            // place working correctly.  So let's prepare the mocks for the calls they should receive.
            foreach (var payloadId in assetPayloads)
            {
                m_PayloadsManagerMock.InSequence(theSequence).Setup(pm => pm.RemovePayloadAsync(payloadId))
                    .Returns(Task.CompletedTask);
            }

            // Do the remove
            bool removeRet = await assetsManager.RemoveAssetAsync(Guid.NewGuid());
            Assert.That(removeRet, Is.False);
            removeRet = await assetsManager.RemoveAssetAsync(assetId);
            Assert.That(removeRet, Is.True);
            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedCollection.Value, Is.Empty);
            }

            // Ensure all the calls in the sequence have been performed
            bool theSequenceCompleted = false;
            m_PayloadsManagerMock.InSequence(theSequence).Setup(pm => pm.RemovePayloadAsync(Guid.Empty))
                .Returns(Task.CompletedTask)
                .Callback(() => theSequenceCompleted = true);
            await m_PayloadsManagerMock.Object.RemovePayloadAsync(Guid.Empty);
            Assert.That(theSequenceCompleted, Is.True);
        }

        [Test]
        public async Task ModifyFromLockedReadOnlyIncrementalCollection()
        {
            Assert.That(m_FileBlobsManagerMock, Is.Not.Null);
            var assetsManager = new AssetsManager(m_LoggerMock.Object, m_PayloadsManagerMock!.Object,
                m_FileBlobsManagerMock!.Object);

            Stream file1Stream = CreateStreamFromString("This is the first file");
            string file1Md5 = ComputeMd5String(file1Stream);

            Catalog launchCatalog = new()
            {
                Payloads = new [] {
                    new LaunchCatalog.Payload()
                    {
                        Name = "Payload",
                        Files = new []
                        {
                            new LaunchCatalog.PayloadFile() { Path = "file1", Md5 = file1Md5 }
                        }
                    }
                },
                Launchables = new [] {
                    new LaunchCatalog.Launchable()
                    {
                        Name = "Cluster Node",
                        Type = "clusterNode",
                        Payloads = new [] { "Payload" }
                    }
                }
            };

            // Prepare all the calls to the mocks adding an asset should result in
            MockSequence theSequence = new();

            Mock<IAssetSource> assetSourceMock = new(MockBehavior.Strict);
            assetSourceMock.InSequence(theSequence).Setup(a => a.GetCatalogAsync()).ReturnsAsync(launchCatalog);

            assetSourceMock.InSequence(theSequence).Setup(a => a.GetFileContentAsync("file1"))
                .ReturnsAsync(new AssetSourceOpenedFile(StreamAt0(file1Stream), file1Stream.Length));
            var fileBlob1Guid = Guid.NewGuid();
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.AddFileBlobAsync(file1Stream, file1Stream.Length, Md5AsGuid(file1Md5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileBlob1Guid);
            m_FileBlobsManagerMock.InSequence(theSequence).Setup(fbm => fbm.LockFileBlob(fileBlob1Guid))
                .ReturnsAsync(CreateFileBlobLock(fileBlob1Guid, Md5AsGuid(file1Md5), 42, file1Stream.Length));

            m_PayloadsManagerMock.InSequence(theSequence)
                .Setup(pm => pm.AddPayloadAsync(It.IsAny<Guid>(), It.IsAny<Payload>()))
                .Returns(Task.CompletedTask);

            bool theSequenceCompleted = false;
            m_FileBlobsManagerMock.InSequence(theSequence)
                .Setup(fbm => fbm.DecreaseFileBlobReferenceAsync(fileBlob1Guid)).Returns(Task.CompletedTask)
                .Callback(() => theSequenceCompleted = true);

            // Add the asset
            AssetPost newAsset = new() { Name = "TestName", Description = "TestDescription" };
            await assetsManager.AddAssetAsync(newAsset, assetSourceMock.Object);

            // Lock it and try to modify it
            using (var lockedCollection = await assetsManager.GetLockedReadOnlyAsync())
            {
                var collection = (IncrementalCollection<Asset>)lockedCollection.Value;

                Assert.Throws<InvalidOperationException>(() => collection.Values.First().SignalChanges(collection));
                Asset toAdd = new(Guid.NewGuid());
                Assert.Throws<InvalidOperationException>(() => collection.Add(toAdd));
                Assert.Throws<InvalidOperationException>(() => collection.Remove(toAdd.Id));
            }

            Assert.That(theSequenceCompleted, Is.True);
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

        static Guid Md5AsGuid(string md5String)
        {
            byte[] bytes = Convert.FromHexString(md5String);
            Assert.That(bytes.Length, Is.EqualTo(16));
            return new Guid(bytes);
        }

        static Stream StreamAt0(Stream toRewind)
        {
            toRewind.Position = 0;
            return toRewind;
        }

        static FileBlobLock CreateFileBlobLock(Guid id, Guid md5, long compressedSize, long size)
        {
            // FileBlobInfo is internal and does not need to be accessed from the outside (except for mock), so let's
            // hack our way in.
            Type[] constructorTypes = { typeof(Guid), typeof(Guid), typeof(long), typeof(long), typeof(Action) };
            var lockConstructor = typeof(FileBlobLock).GetConstructor(
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic, constructorTypes);
            Assert.That(lockConstructor, Is.Not.Null);

            void DoNothingAction() { }
            object[] constructorArgs = new object[] { id, md5, compressedSize, size, (Action) DoNothingAction };
            FileBlobLock ret = (FileBlobLock)lockConstructor!.Invoke(constructorArgs);
            return ret;
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "AssetsManagerTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        List<string> m_StorageFolders = new();
        Mock<ILogger> m_LoggerMock = new();
        Mock<FileBlobsManager>? m_FileBlobsManagerMock;
        Mock<PayloadsManager>? m_PayloadsManagerMock;
    }
}
