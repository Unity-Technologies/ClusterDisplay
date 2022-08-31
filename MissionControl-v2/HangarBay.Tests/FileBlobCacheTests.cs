using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class FileBlobCacheTests
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
                catch { }
            }
        }

        [Test]
        public void IncDecUsageCountWOStorage()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);

            var fileBlob1Id = Guid.NewGuid();
            var fileBlob1CompressedSize = Random.Shared.Next(1024, 1024 * 1024);
            var fileBlob1ContentSize = fileBlob1CompressedSize * 2;
            fileBlobCache.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize);
            fileBlobCache.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize);

            var fileBlob2Id = Guid.NewGuid();
            var fileBlob2CompressedSize = Random.Shared.Next(1024, 1024 * 1024);
            var fileBlob2ContentSize = fileBlob2CompressedSize * 2;
            fileBlobCache.IncreaseUsageCount(fileBlob2Id, fileBlob2CompressedSize, fileBlob2ContentSize);
            fileBlobCache.IncreaseUsageCount(fileBlob2Id, fileBlob2CompressedSize, fileBlob2ContentSize);

            Assert.That(() => fileBlobCache.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize+1, fileBlob1ContentSize),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(() => fileBlobCache.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize+1),
                        Throws.TypeOf<ArgumentException>());

            fileBlobCache.DecreaseUsageCount(fileBlob1Id);
            fileBlobCache.DecreaseUsageCount(fileBlob1Id);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(0));
            fileBlobCache.DecreaseUsageCount(fileBlob1Id);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains(fileBlob1Id.ToString()), Is.True);
            fileBlobCache.DecreaseUsageCount(fileBlob2Id);
            fileBlobCache.DecreaseUsageCount(fileBlob2Id);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            fileBlobCache.DecreaseUsageCount(fileBlob2Id);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(2));
            Assert.That(m_LoggerStub.Messages[1].Content.Contains(fileBlob2Id.ToString()), Is.True);
            m_LoggerStub.Messages.Clear();
        }

        [Test]
        public async Task SimpleCopyFile()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);

            var storageFolderConfig = new StorageFolderConfig();
            storageFolderConfig.Path = GetNewStorageFolder();
            storageFolderConfig.MaximumSize = 1024;
            fileBlobCache.AddStorageFolder(storageFolderConfig);

            Guid fetchedGuid = Guid.Empty;
            string fetchPath = "";
            fileBlobCache.FetchFileCallback = (Guid blobId, string path, object? _) =>
                {
                    fetchedGuid = blobId;
                    fetchPath = path;
                    return Task.CompletedTask;
                };

            string copyFrom = "";
            string copyTo = "";
            fileBlobCache.CopyFileCallback = (string from, string to, object? _) =>
                {
                    copyFrom = from;
                    copyTo = to;
                    return Task.CompletedTask;
                };

            Guid fileBobToAsk = Guid.Parse("570A07FD-AAFA-4F73-9377-05F2D76F5A87");
            string expectedFetchPath = Path.Combine(storageFolderConfig.Path, "57", "0a", fileBobToAsk.ToString());
            string copyToPath = "c:\\temp\\patate.exe";

            Assert.That( async () => await fileBlobCache.CopyFileToAsync(fileBobToAsk, "c:\\temp\\patate.exe"),
                         Throws.TypeOf<ArgumentException>());

            fileBlobCache.IncreaseUsageCount(fileBobToAsk, 28, 42);

            await fileBlobCache.CopyFileToAsync(fileBobToAsk, copyToPath);

            Assert.That(fetchedGuid, Is.EqualTo(fileBobToAsk));
            Assert.That(fetchPath, Is.EqualTo(expectedFetchPath));
            Assert.That(copyFrom, Is.EqualTo(expectedFetchPath));
            Assert.That(copyTo, Is.EqualTo(copyToPath));

            CompareStatus(fileBlobCache,
                new[] { new StorageFolderStatus() { Path = storageFolderConfig.Path, CurrentSize = 28, UnreferencedSize = 0,
                                                    ZombiesSize = 0, MaximumSize = 1024 } });
        }

        [Test]
        public async Task EvictWhenLowOnSpace()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            var folder2000Config = new StorageFolderConfig();
            folder2000Config.Path = GetNewStorageFolder();
            folder2000Config.MaximumSize = 2000;
            fileBlobCache.AddStorageFolder(folder2000Config);

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 15, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 15, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob4 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob4, 100, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob5 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob5, 1000, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob5, "C:\\Temp\\Blob5");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 1115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob6 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob6, 1000, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob6, "C:\\Temp\\Blob6");

            // Evicts (in order they were added)
            // fileBlob1 (10 bytes saved in folder1000) (100 left)
            // fileBlob2 (15 bytes saved in folder2000) (1100 left)
            // fileBlob3 (100 bytes saved in folder2000) (1000 left)
            // Now enough space in folder2000

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 100, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 2000, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            // Now decrement fileBlob6, this should cause the file to become unreferenced and so it will be evicted before anything
            // else.
            fileBlobCache.DecreaseUsageCount(fileBlob6);

            // Add a small file that fits in folder1000, so it shouldn't cause any eviction
            Guid fileBlob7 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob7, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob7, "C:\\Temp\\Blob7");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 2000, UnreferencedSize = 1000,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob8 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob8, 900, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob8, "C:\\Temp\\Blob8");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 1900, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });
        }

        class TestException: Exception
        {
        }

        [Test]
        public async Task FetchFail()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            bool fetchShouldSucceed = true;
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    if (fetchShouldSucceed)
                    {
                        File.WriteAllText(cachePath, "Some content");
                        return Task.CompletedTask;
                    }
                    else
                    {
                        throw new TestException();
                    }
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 100, 10000);

            fetchShouldSucceed = false;
            Assert.That(async () => await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2"),
                        Throws.TypeOf<TestException>());

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            fetchShouldSucceed = true;
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        [Test]
        public async Task CopyFail()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            bool copyShouldSucceed = true;
            int copyStarted = 0;
            int copyFinished = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyStarted);
                    if (copyShouldSucceed)
                    {
                        Interlocked.Increment(ref copyFinished);
                        return Task.CompletedTask;
                    }
                    else
                    {
                        throw new TestException();
                    }
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");

            Assert.That(copyStarted, Is.EqualTo(1));
            Assert.That(copyFinished, Is.EqualTo(1));

            copyShouldSucceed = false;
            Assert.That(async () => await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB"),
                        Throws.TypeOf<TestException>());

            Assert.That(copyStarted, Is.EqualTo(2));
            Assert.That(copyFinished, Is.EqualTo(1));

            copyShouldSucceed = true;
            await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");

            Assert.That(copyStarted, Is.EqualTo(3));
            Assert.That(copyFinished, Is.EqualTo(2));
        }

        [Test]
        public async Task SimultaneousCopyOfSameFileWaitingOnFetch()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            var fetchTCS = new TaskCompletionSource();
            int fetchCallCount = 0;
            fileBlobCache.FetchFileCallback = async (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCallCount);
                    await fetchTCS.Task;
                    File.WriteAllText(cachePath, "Some content");
                };
            int copyCallCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCallCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 10, 10000);

            var fileA = fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1-a");
            var fileB = fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1-b");

            await Task.Delay(50);
            Assert.That(fileA.IsCompleted, Is.False);
            Assert.That(fileB.IsCompleted, Is.False);

            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (fetchCallCount != 1 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(fetchCallCount, Is.EqualTo(1));
            Assert.That(copyCallCount, Is.EqualTo(0));

            // Remark, space for the file is reserved in the storage folder even if it is not really there yet...
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            fetchTCS.SetResult();
            await fileA;
            await fileB;

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Assert.That(fetchCallCount, Is.EqualTo(1));
            Assert.That(copyCallCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SimultaneousFetch()
        {
            Guid fileBlob1 = Guid.NewGuid();
            Guid fileBlob2 = Guid.NewGuid();
            var completedFetchTCS = new Dictionary<Guid, TaskCompletionSource>();
            completedFetchTCS[fileBlob1] = new TaskCompletionSource();
            completedFetchTCS[fileBlob2] = new TaskCompletionSource();

            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCallCount = 0;
            fileBlobCache.FetchFileCallback = async (Guid fileBlobId, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCallCount);
                    if (completedFetchTCS.TryGetValue(fileBlobId, out var tcs))
                    {
                        await tcs.Task;
                    }
                    File.WriteAllText(cachePath, "Some content");
                };
            int copyCallCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCallCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            fileBlobCache.IncreaseUsageCount(fileBlob1, 10, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob2, 100, 10000);

            var fileA = fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");
            var fileB = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            await Task.Delay(50);
            Assert.That(fileA.IsCompleted, Is.False);
            Assert.That(fileB.IsCompleted, Is.False);

            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (fetchCallCount != 2 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(fetchCallCount, Is.EqualTo(2));
            Assert.That(copyCallCount, Is.EqualTo(0));

            // Remark, space for the file is reserved in the storage folder even if it is not really there yet...
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            completedFetchTCS[fileBlob1].SetResult();
            await fileA;

            Assert.That(fileB.IsCompleted, Is.False);
            Assert.That(fetchCallCount, Is.EqualTo(2));
            Assert.That(copyCallCount, Is.EqualTo(1));

            completedFetchTCS[fileBlob2].SetResult();
            await fileB;
            Assert.That(fetchCallCount, Is.EqualTo(2));
            Assert.That(copyCallCount, Is.EqualTo(2));

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        [Test]
        public void AddStorageFolderErrors()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folderConfig = new StorageFolderConfig();
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            folderConfig.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folderConfig);

            // Double add should fail
            Assert.That(() => fileBlobCache.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // Double add through a different path.
            // We can get through this case through symbolic links or case insensitive filenames.  Since Windows
            // requires an elevated privilege to create symbolic links and most file systems under windows are case
            // insensitive let's simply change the case of the folder.
            string aliasPath = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                aliasPath = folderConfig.Path.ToLower();
            }
            else
            {
                Assert.Fail("TODO");
            }
            var aliasConfig = new StorageFolderConfig();
            aliasConfig.Path = aliasPath;
            aliasConfig.MaximumSize = folderConfig.MaximumSize;
            Assert.That(() => fileBlobCache.AddStorageFolder(aliasConfig), Throws.TypeOf<ArgumentException>());

            // Same for equivalent path
            folderConfig.Path = Path.Combine(folderConfig.Path, "Patate", "..");
            Assert.That(() => fileBlobCache.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // And try a non empty folder
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            var filePath = Path.Combine(folderConfig.Path, "SomeFile.txt");
            File.WriteAllText(filePath, "Some content");
            Assert.That(() => fileBlobCache.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task DeleteStorageFolder()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            fileBlobCache.DecreaseUsageCount(fileBlob2);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Assert.That(() => fileBlobCache.DeleteStorageFolderAsync(GetNewStorageFolder()),
                        Throws.TypeOf<ArgumentException>());
            await fileBlobCache.DeleteStorageFolderAsync(folder1000Config.Path);

            CompareStatus(fileBlobCache, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task DeleteStorageFolderBlockWhileFileFetched()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            var fetchTCS = new TaskCompletionSource();
            fetchTCS.SetResult(); // We want the first ones to immediately proceed
            fileBlobCache.FetchFileCallback = async (Guid _, string cachePath, object? _) =>
                {
                    await fetchTCS.Task;
                    File.WriteAllText(cachePath, "Some content");
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);

            fetchTCS = new TaskCompletionSource();
            var copyBlob2 = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            var copyBlob3 = fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
            var deleteStorageTask = fileBlobCache.DeleteStorageFolderAsync(folder1000Config.Path);

            await Task.Delay(50);

            // The deleting folder should have been able to delete 1 file but still waiting for the 2 other ones
            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (fileBlobCache.GetStorageFolderStatus()[0].CurrentSize != 110 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 0 }
            });
            Assert.That(deleteStorageTask.IsCompleted, Is.False);
            
            //// Unblock the fetch
            fetchTCS.SetResult();
            await deleteStorageTask;
            Assert.That(deleteStorageTask.IsCompleted, Is.True);
            Assert.That(copyBlob2.IsCompleted, Is.True);
            Assert.That(copyBlob3.IsCompleted, Is.True);

            // All storage folders should be gone
            CompareStatus(fileBlobCache, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task DeleteStorageFolderBlockWhileCopying()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            copyTCS.SetResult(); // We want the first ones to immediately proceed
            fileBlobCache.CopyFileCallback = async (string _, string _, object? _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);

            copyTCS = new TaskCompletionSource();
            var copyBlob2a = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2a");
            var copyBlob2b = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2b");
            var copyBlob2c = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2c");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            var copyBlob3 = fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
            var deleteStorageTask = fileBlobCache.DeleteStorageFolderAsync(folder1000Config.Path);

            await Task.Delay(50);

            // The deleting folder should have been able to delete 1 file but still waiting for the 2 other ones
            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (fileBlobCache.GetStorageFolderStatus()[0].CurrentSize != 110 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 0 }
            });
            Assert.That(deleteStorageTask.IsCompleted, Is.False);
            
            //// Unblock the fetch
            copyTCS.SetResult();
            await deleteStorageTask;
            Assert.That(deleteStorageTask.IsCompleted, Is.True);
            Assert.That(copyBlob2a.IsCompleted, Is.True);
            Assert.That(copyBlob2b.IsCompleted, Is.True);
            Assert.That(copyBlob2c.IsCompleted, Is.True);
            Assert.That(copyBlob3.IsCompleted, Is.True);

            // All storage folders should be gone
            CompareStatus(fileBlobCache, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task RemoveStorageFolder()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            fileBlobCache.DecreaseUsageCount(fileBlob2);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Remove the storage folder
            Assert.That(() => fileBlobCache.RemoveStorageFolderAsync(GetNewStorageFolder()),
                        Throws.TypeOf<ArgumentException>());
            await fileBlobCache.RemoveStorageFolderAsync(folder1000Config.Path);
            CompareStatus(fileBlobCache, new StorageFolderStatus[] {});

            // Add it back
            fileBlobCache.AddStorageFolder(folder1000Config);
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Ask for the files just to be sure everything is ok
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Assert.Fail("Everything should be present in the folder so we shouldn't need to be called.");
                    return Task.CompletedTask;
                };
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1b");
            Assert.That(async () => await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2b"),
                Throws.TypeOf<ArgumentException>());
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2b");
            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3b");
        }

        [Test]
        public async Task RemoveStorageFolderWhileInUse()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            var fetchTcs = new TaskCompletionSource();
            fetchTcs.SetResult();
            fileBlobCache.FetchFileCallback = async (Guid _, string cachePath, object? _) =>
                {
                    await fetchTcs.Task;
                    File.WriteAllText(cachePath, "Some content");
                };
            var copyTcs = new TaskCompletionSource();
            copyTcs.SetResult();
            fileBlobCache.CopyFileCallback = async (string _, string _, object? _) =>
                {
                    await copyTcs.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);

            copyTcs = new();
            var fileBlob2Task = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);

            fetchTcs = new();
            var fileBlob3Task = fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Remove the storage folder
            Assert.That(() => fileBlobCache.RemoveStorageFolderAsync(GetNewStorageFolder()),
                        Throws.TypeOf<ArgumentException>());
            var removeStorageTask = fileBlobCache.RemoveStorageFolderAsync(folder1000Config.Path);

            await Task.Delay(50);
            Assert.That(fileBlob2Task.IsCompleted, Is.False);
            Assert.That(fileBlob3Task.IsCompleted, Is.False);
            Assert.That(removeStorageTask.IsCompleted, Is.False);
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Unblock everything
            fetchTcs.SetResult();
            copyTcs.SetResult();
            await fileBlob2Task;
            await fileBlob3Task;

            await removeStorageTask;
            CompareStatus(fileBlobCache, new StorageFolderStatus[] {});

            // Add it back
            fileBlobCache.AddStorageFolder(folder1000Config);
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Ask for the files just to be sure everything is ok
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Assert.Fail("Everything should be present in the folder so we shouldn't need to be called.");
                    return Task.CompletedTask;
                };
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1b");
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2b");
            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3b");
        }

        [Test]
        public async Task UpdateStorageFolder()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 10, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 100, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 600, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 710, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob4 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob4, 5, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 715, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            folder1000Config.MaximumSize = 610;
            fileBlobCache.UpdateStorageFolder(folder1000Config);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 605, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 610 }
            });

            folder1000Config.Path = GetNewStorageFolder();
            Assert.That(() => fileBlobCache.UpdateStorageFolder(folder1000Config), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task CacheFull()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            fileBlobCache.CopyFileCallback = async (string _, string _, object? _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 750, 10000);

            var copyBlob1 = fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            // Ask for a file that can only fit in cache if fileBlob1 is not in use (but it is)
            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 1000, 10000);

            var copyBlob2 = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Assert.That(async () => await copyBlob2, Throws.TypeOf<InvalidOperationException>());

            // Conclude fileBlob1
            copyTCS.SetResult();
            await copyBlob1;

            // And now we should be able to complete fileBlob2
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");
        }

        [Test]
        public async Task UseUnreferenced()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob, 750, 10000);

            await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(1));

            // Remove last reference to the blob
            fileBlobCache.DecreaseUsageCount(fileBlob);

            // While at it, to decrease reference yet another time, it should generate a warning message.
            Assert.That(m_LoggerStub.Messages, Is.Empty);
            fileBlobCache.DecreaseUsageCount(fileBlob);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            m_LoggerStub.Messages.Clear();

            // As a result we should get an exception when asking to copy that blob
            Assert.That(async () => await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB"),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(1));

            // Try to increase usage count (but with the wrong size, should fail since a file blob should always have
            // the same size).
            Assert.That(() => fileBlobCache.IncreaseUsageCount(fileBlob, 751, 10000),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(() => fileBlobCache.IncreaseUsageCount(fileBlob, 750, 10001),
                        Throws.TypeOf<ArgumentException>());

            // But should succeed if sizes matches
            fileBlobCache.IncreaseUsageCount(fileBlob, 750, 10000);

            // And now copying should work
            await fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(2));
        }
        
        [Test]
        public async Task DecreaseUsageOfInUse()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            fileBlobCache.CopyFileCallback = async (string _, string _, object? _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob, 10, 10000);

            var copyBlobA = fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");
            var copyBlobB = fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB");
            var copyBlobC = fileBlobCache.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Decrease usage while in use, should generate a warning and postpone work until not in use anymore
            Assert.That(m_LoggerStub.Messages, Is.Empty);
            fileBlobCache.DecreaseUsageCount(fileBlob);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains(fileBlob.ToString()), Is.True);
            m_LoggerStub.Messages.Clear();

            await Task.Delay(50);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Complete the copies
            copyTCS.SetResult();
            await copyBlobA;
            await copyBlobB;
            await copyBlobC;

            // The file should now soon be moved to unreferenced files
            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (fileBlobCache.GetStorageFolderStatus()[0].UnreferencedSize != 10 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }
        
        [Test]
        public async Task SaveAndLoadStorageFolder()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            fileBlobCache.DecreaseUsageCount(fileBlob2);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            fileBlobCache.PersistStorageFolderStates();

            // Rebuild a new FileBlobCache from that saved content
            var oldFileBlobCache = fileBlobCache;
            fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = oldFileBlobCache.FetchFileCallback;
            fileBlobCache.CopyFileCallback = oldFileBlobCache.CopyFileCallback;
            oldFileBlobCache = null;

            // Reference 2 and 3 instead of 1 and 3 to test that list of referenced and non referenced files gets
            // updated both ways.
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            folder1000Config.MaximumSize *= 2;
            fileBlobCache.AddStorageFolder(folder1000Config);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 1,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Test that we can copy files after reload.
            var copyTasks = new List<Task>();
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(6));
        }

        [Test]
        public async Task SaveAndLoadStorageFolderWhileInUse()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            TaskCompletionSource fetchTCS = new();
            fetchTCS.SetResult();
            fileBlobCache.FetchFileCallback = async (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    await fetchTCS.Task;
                };
            int copyCount = 0;
            TaskCompletionSource copyTCS = new();
            copyTCS.SetResult();
            fileBlobCache.CopyFileCallback = async (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            copyTCS = new();
            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            var blob2Task = fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (copyCount != 2 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(copyCount, Is.EqualTo(2));

            fetchTCS = new();
            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            var blob3Task = fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            maxWaitTimer.Start();
            while (fetchCount != 3 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(2));

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            fileBlobCache.PersistStorageFolderStates();

            // Unblock everything (the goal was anyway to save while in use, so this is done)
            copyTCS.SetResult();
            fetchTCS.SetResult();
            await blob2Task;
            await blob3Task;

            // Rebuild a new FileBlobCache from that saved content
            var oldFileBlobCache = fileBlobCache;
            fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = oldFileBlobCache.FetchFileCallback;
            fileBlobCache.CopyFileCallback = oldFileBlobCache.CopyFileCallback;
            oldFileBlobCache = null;

            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            fileBlobCache.AddStorageFolder(folder1000Config);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Test that we can copy files after reload.
            var copyTasks = new List<Task>();
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(6));
        }

        [Test]
        public async Task FileInTwoStoragesOnReload()
        {
            // Prepare first FileBlobCache with 2 files
            var fileBlobCache1 = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            fileBlobCache1.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            fileBlobCache1.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folderAConfig = new StorageFolderConfig();
            folderAConfig.Path = GetNewStorageFolder();
            folderAConfig.MaximumSize = 2000;
            fileBlobCache1.AddStorageFolder(folderAConfig);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache1.IncreaseUsageCount(fileBlob1, 1, 10000);
            await fileBlobCache1.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache1.IncreaseUsageCount(fileBlob2, 10, 10000);
            await fileBlobCache1.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache1.IncreaseUsageCount(fileBlob3, 100, 10000);
            await fileBlobCache1.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(fileBlobCache1, new[] {
                new StorageFolderStatus() { Path = folderAConfig.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Save
            fileBlobCache1.PersistStorageFolderStates();

            // Prepare second FileBlobCache with again 2 files (one is the same as in the first one)
            var fileBlobCache2 = new FileBlobCache(m_LoggerStub);
            fileBlobCache2.FetchFileCallback = fileBlobCache1.FetchFileCallback;
            fileBlobCache2.CopyFileCallback = fileBlobCache1.CopyFileCallback;

            var folderBConfig = new StorageFolderConfig();
            folderBConfig.Path = GetNewStorageFolder();
            folderBConfig.MaximumSize = 2000;
            fileBlobCache2.AddStorageFolder(folderBConfig);

            fileBlobCache2.IncreaseUsageCount(fileBlob2, 10, 10000);
            await fileBlobCache2.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            fileBlobCache2.IncreaseUsageCount(fileBlob3, 100, 10000);
            await fileBlobCache2.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Guid fileBlob4 = Guid.NewGuid();
            fileBlobCache2.IncreaseUsageCount(fileBlob4, 1000, 10000);
            await fileBlobCache2.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            Assert.That(fetchCount, Is.EqualTo(6));
            Assert.That(copyCount, Is.EqualTo(6));

            CompareStatus(fileBlobCache2, new[] {
                new StorageFolderStatus() { Path = folderBConfig.Path, CurrentSize = 1110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Save
            fileBlobCache2.PersistStorageFolderStates();

            // Create a new FileBlobCache using both storage folders
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = fileBlobCache1.FetchFileCallback;
            fileBlobCache.CopyFileCallback = fileBlobCache1.CopyFileCallback;

            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob4, 1000, 10000);

            fileBlobCache.AddStorageFolder(folderAConfig);
            fileBlobCache.DecreaseUsageCount(fileBlob3);
            Assert.That(m_LoggerStub.Messages, Is.Empty);
            fileBlobCache.AddStorageFolder(folderBConfig);
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(2));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains(fileBlob2.ToString()), Is.True);
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("found in both"), Is.True);
            Assert.That(m_LoggerStub.Messages[1].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[1].Content.Contains(fileBlob3.ToString()), Is.True);
            Assert.That(m_LoggerStub.Messages[1].Content.Contains("found in both"), Is.True);
            m_LoggerStub.Messages.Clear();

            var copyTasks = new List<Task>();
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( fileBlobCache.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(6));
            Assert.That(copyCount, Is.EqualTo(9));

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folderAConfig.Path, CurrentSize = 111, UnreferencedSize = 100,
                                            ZombiesSize = 0, MaximumSize = 2000 },
                new StorageFolderStatus() { Path = folderBConfig.Path, CurrentSize = 1000, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });
        }

        [Test]
        public async Task ReloadInSmaller()
        {
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            fileBlobCache.PersistStorageFolderStates();

            // Rebuild a new FileBlobCache from that saved content
            var oldFileBlobCache = fileBlobCache;
            fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = oldFileBlobCache.FetchFileCallback;
            fileBlobCache.CopyFileCallback = oldFileBlobCache.CopyFileCallback;
            oldFileBlobCache = null;

            // Reference 2 and 3 instead of 1 and 3 to test that list of referenced and non referenced files gets
            // updated both ways.
            fileBlobCache.IncreaseUsageCount(fileBlob1, 1, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob2, 10, 10000);
            fileBlobCache.IncreaseUsageCount(fileBlob3, 100, 10000);
            folder1000Config.MaximumSize = 101;
            fileBlobCache.AddStorageFolder(folder1000Config);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 100, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 101 }
            });
        }
        
        [Test]
        public async Task DealingWithZombies()
        {
            // Create a cache with some content
            var fileBlobCache = new FileBlobCache(m_LoggerStub);
            int fetchCount = 0;
            List<string> fetchedFiles = new();
            fileBlobCache.FetchFileCallback = (Guid _, string cachePath, object? _) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    StringBuilder builder = new();
                    File.WriteAllText(cachePath, "Some content");
                    fetchedFiles.Add(cachePath);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            fileBlobCache.CopyFileCallback = (string _, string _, object? _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            fileBlobCache.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob1, 100, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob2, 300, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            fileBlobCache.IncreaseUsageCount(fileBlob3, 500, 10000);
            await fileBlobCache.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(fetchedFiles.Count, Is.EqualTo(3));
            Assert.That(fetchedFiles[1].EndsWith(fileBlob2.ToString()), Is.True);
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 900, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Lock a file so that it cannot be deleted
            Assert.That(m_LoggerStub.Messages, Is.Empty);
            Guid fileBlob4 = Guid.NewGuid();
            using (var openedFile = File.Open(fetchedFiles[1], FileMode.Open))
            {
                // Ask for some content that will trigger cache eviction -> delete -> causing a zombie from the locked file.
                fileBlobCache.IncreaseUsageCount(fileBlob4, 400, 10000);
                await fileBlobCache.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");
            }
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains(fileBlob2.ToString()), Is.True);
            m_LoggerStub.Messages.Clear();

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 912, UnreferencedSize = 0,
                                            ZombiesSize = 12, MaximumSize = 1000 }
            });

            // Save
            fileBlobCache.PersistStorageFolderStates();

            // Rebuild a new FileBlobCache from that saved content (will now get rid of zombies since the file is not
            // locked anymore).
            var oldFileBlobCache = fileBlobCache;
            fileBlobCache = new FileBlobCache(m_LoggerStub);
            fileBlobCache.FetchFileCallback = oldFileBlobCache.FetchFileCallback;
            fileBlobCache.CopyFileCallback = oldFileBlobCache.CopyFileCallback;
            oldFileBlobCache = null;
            fileBlobCache.AddStorageFolder(folder1000Config);

            CompareStatus(fileBlobCache, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 900, UnreferencedSize = 900,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "FileBlobCacheTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        void CompareStatus(FileBlobCache manager, StorageFolderStatus[] expectedStatuses)
        {
            var currentStatuses = manager.GetStorageFolderStatus();

            foreach (var expected in expectedStatuses)
            {
                var current = currentStatuses.Where(s => s.Path == expected.Path).FirstOrDefault();
                Assert.That(current, Is.Not.Null);
                Assert.That(current.CurrentSize, Is.EqualTo(expected.CurrentSize));
                Assert.That(current.UnreferencedSize, Is.EqualTo(expected.UnreferencedSize));
                Assert.That(current.ZombiesSize, Is.EqualTo(expected.ZombiesSize));
                Assert.That(current.MaximumSize, Is.EqualTo(expected.MaximumSize));
                current.Path = "";
            }

            foreach (var current in currentStatuses)
            {
                Assert.That(current.Path, Is.EqualTo(""));
            }
        }

        List<string> m_StorageFolders = new();
        LoggerStub m_LoggerStub = new();
    }
}
