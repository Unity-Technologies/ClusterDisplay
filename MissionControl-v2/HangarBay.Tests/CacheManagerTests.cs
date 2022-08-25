using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class CacheManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(_loggerStub.Messages, Is.Empty);

            foreach (string folder in _storageFolders)
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
            var cacheManager = new CacheManager(_loggerStub);

            var fileBlob1Id = Guid.NewGuid();
            var fileBlob1CompressedSize = Random.Shared.Next(1024, 1024 * 1024);
            var fileBlob1ContentSize = fileBlob1CompressedSize * 2;
            cacheManager.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize);
            cacheManager.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize);

            var fileBlob2Id = Guid.NewGuid();
            var fileBlob2CompressedSize = Random.Shared.Next(1024, 1024 * 1024);
            var fileBlob2ContentSize = fileBlob2CompressedSize * 2;
            cacheManager.IncreaseUsageCount(fileBlob2Id, fileBlob2CompressedSize, fileBlob2ContentSize);
            cacheManager.IncreaseUsageCount(fileBlob2Id, fileBlob2CompressedSize, fileBlob2ContentSize);

            Assert.That(() => cacheManager.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize+1, fileBlob1ContentSize),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(() => cacheManager.IncreaseUsageCount(fileBlob1Id, fileBlob1CompressedSize, fileBlob1ContentSize+1),
                        Throws.TypeOf<ArgumentException>());

            cacheManager.DecreaseUsageCount(fileBlob1Id);
            cacheManager.DecreaseUsageCount(fileBlob1Id);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(0));
            cacheManager.DecreaseUsageCount(fileBlob1Id);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(_loggerStub.Messages[0].Content.Contains(fileBlob1Id.ToString()), Is.True);
            cacheManager.DecreaseUsageCount(fileBlob2Id);
            cacheManager.DecreaseUsageCount(fileBlob2Id);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(1));
            cacheManager.DecreaseUsageCount(fileBlob2Id);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(2));
            Assert.That(_loggerStub.Messages[1].Content.Contains(fileBlob2Id.ToString()), Is.True);
            _loggerStub.Messages.Clear();
        }

        [Test]
        public async Task SimpleCopyFile()
        {
            var cacheManager = new CacheManager(_loggerStub);

            var storageFolderConfig = new StorageFolderConfig();
            storageFolderConfig.Path = GetNewStorageFolder();
            storageFolderConfig.MaximumSize = 1024;
            cacheManager.AddStorageFolder(storageFolderConfig);

            Guid fetchedGuid = Guid.Empty;
            string fetchPath = "";
            cacheManager.FetchFileCallback = (Guid blobId, string path) =>
                {
                    fetchedGuid = blobId;
                    fetchPath = path;
                    return Task.CompletedTask;
                };

            string copyFrom = "";
            string copyTo = "";
            cacheManager.CopyFileCallback = (string from, string to) =>
                {
                    copyFrom = from;
                    copyTo = to;
                    return Task.CompletedTask;
                };

            Guid fileBobToAsk = Guid.Parse("570A07FD-AAFA-4F73-9377-05F2D76F5A87");
            string expectedFetchPath = Path.Combine(storageFolderConfig.Path, "57", "0a", fileBobToAsk.ToString());
            string copyToPath = "c:\\temp\\patate.exe";

            Assert.That( async () => await cacheManager.CopyFileToAsync(fileBobToAsk, "c:\\temp\\patate.exe"),
                         Throws.TypeOf<ArgumentException>());

            cacheManager.IncreaseUsageCount(fileBobToAsk, 28, 42);

            await cacheManager.CopyFileToAsync(fileBobToAsk, copyToPath);

            Assert.That(fetchedGuid, Is.EqualTo(fileBobToAsk));
            Assert.That(fetchPath, Is.EqualTo(expectedFetchPath));
            Assert.That(copyFrom, Is.EqualTo(expectedFetchPath));
            Assert.That(copyTo, Is.EqualTo(copyToPath));

            CompareStatus(cacheManager,
                new[] { new StorageFolderStatus() { Path = storageFolderConfig.Path, CurrentSize = 28, UnreferencedSize = 0,
                                                    ZombiesSize = 0, MaximumSize = 1024 } });
        }

        [Test]
        public async Task EvictWhenLowOnSpace()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            var folder2000Config = new StorageFolderConfig();
            folder2000Config.Path = GetNewStorageFolder();
            folder2000Config.MaximumSize = 2000;
            cacheManager.AddStorageFolder(folder2000Config);

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 15, 10000);

            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 15, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);

            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob4 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob4, 100, 10000);

            await cacheManager.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob5 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob5, 1000, 10000);

            await cacheManager.CopyFileToAsync(fileBlob5, "C:\\Temp\\Blob5");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 1115, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob6 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob6, 1000, 10000);

            await cacheManager.CopyFileToAsync(fileBlob6, "C:\\Temp\\Blob6");

            // Evicts (in order they were added)
            // fileBlob1 (10 bytes saved in folder1000) (100 left)
            // fileBlob2 (15 bytes saved in folder2000) (1100 left)
            // fileBlob3 (100 bytes saved in folder2000) (1000 left)
            // Now enough space in folder2000

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 100, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 2000, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            // Now decrement fileBlob6, this should cause the file to become unreferenced and so it will be evicted before anything
            // else.
            cacheManager.DecreaseUsageCount(fileBlob6);

            // Add a small file that fits in folder1000, so it shouldn't cause any eviction
            Guid fileBlob7 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob7, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob7, "C:\\Temp\\Blob7");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 },
                new StorageFolderStatus() { Path = folder2000Config.Path, CurrentSize = 2000, UnreferencedSize = 1000,
                                            ZombiesSize = 0, MaximumSize = 2000 },
            });

            Guid fileBlob8 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob8, 900, 10000);

            await cacheManager.CopyFileToAsync(fileBlob8, "C:\\Temp\\Blob8");

            CompareStatus(cacheManager, new[] {
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
            var cacheManager = new CacheManager(_loggerStub);
            bool fetchShouldSucceed = true;
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
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
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 100, 10000);

            fetchShouldSucceed = false;
            Assert.That(async () => await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2"),
                        Throws.TypeOf<TestException>());

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            fetchShouldSucceed = true;
            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        [Test]
        public async Task CopyFail()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            bool copyShouldSucceed = true;
            int copyStarted = 0;
            int copyFinished = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
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
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");

            Assert.That(copyStarted, Is.EqualTo(1));
            Assert.That(copyFinished, Is.EqualTo(1));

            copyShouldSucceed = false;
            Assert.That(async () => await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB"),
                        Throws.TypeOf<TestException>());

            Assert.That(copyStarted, Is.EqualTo(2));
            Assert.That(copyFinished, Is.EqualTo(1));

            copyShouldSucceed = true;
            await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");

            Assert.That(copyStarted, Is.EqualTo(3));
            Assert.That(copyFinished, Is.EqualTo(2));
        }

        [Test]
        public async Task SimultaneousCopyOfSameFileWaitingOnFetch()
        {
            var cacheManager = new CacheManager(_loggerStub);
            var fetchTCS = new TaskCompletionSource();
            int fetchCallCount = 0;
            cacheManager.FetchFileCallback = async (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCallCount);
                    await fetchTCS.Task;
                    File.WriteAllText(cachePath, "Some content");
                };
            int copyCallCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCallCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 10, 10000);

            var fileA = cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1-a");
            var fileB = cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1-b");

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
            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            fetchTCS.SetResult();
            await fileA;
            await fileB;

            CompareStatus(cacheManager, new[] {
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

            var cacheManager = new CacheManager(_loggerStub);
            int fetchCallCount = 0;
            cacheManager.FetchFileCallback = async (Guid fileBlobId, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCallCount);
                    if (completedFetchTCS.TryGetValue(fileBlobId, out var tcs))
                    {
                        await tcs.Task;
                    }
                    File.WriteAllText(cachePath, "Some content");
                };
            int copyCallCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCallCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            cacheManager.IncreaseUsageCount(fileBlob1, 10, 10000);
            cacheManager.IncreaseUsageCount(fileBlob2, 100, 10000);

            var fileA = cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");
            var fileB = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

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
            CompareStatus(cacheManager, new[] {
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

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        [Test]
        public void AddStorageFolderErrors()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folderConfig = new StorageFolderConfig();
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            folderConfig.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folderConfig);

            // Double add should fail
            Assert.That(() => cacheManager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // Same for equivalent path
            folderConfig.Path = Path.Combine(folderConfig.Path, "Patate", "..");
            Assert.That(() => cacheManager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());

            // And try a non empty folder
            folderConfig.Path = GetNewStorageFolder();
            Directory.CreateDirectory(folderConfig.Path);
            var filePath = Path.Combine(folderConfig.Path, "SomeFile.txt");
            File.WriteAllText(filePath, "Some content");
            Assert.That(() => cacheManager.AddStorageFolder(folderConfig), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task DeleteStorageFolder()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);

            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            cacheManager.DecreaseUsageCount(fileBlob2);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Assert.That(() => cacheManager.DeleteStorageFolderAsync(GetNewStorageFolder()),
                        Throws.TypeOf<ArgumentException>());
            await cacheManager.DeleteStorageFolderAsync(folder1000Config.Path);

            CompareStatus(cacheManager, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task DeleteStorageFolderBlockWhileFileFetched()
        {
            var cacheManager = new CacheManager(_loggerStub);
            var fetchTCS = new TaskCompletionSource();
            fetchTCS.SetResult(); // We want the first ones to immediately proceed
            cacheManager.FetchFileCallback = async (Guid _, string cachePath) =>
                {
                    await fetchTCS.Task;
                    File.WriteAllText(cachePath, "Some content");
                };
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);

            fetchTCS = new TaskCompletionSource();
            var copyBlob2 = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);

            var copyBlob3 = cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
            var deleteStorageTask = cacheManager.DeleteStorageFolderAsync(folder1000Config.Path);

            await Task.Delay(50);

            // The deleting folder should have been able to delete 1 file but still waiting for the 2 other ones
            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (cacheManager.GetStorageFolderStatus()[0].CurrentSize != 110 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(cacheManager, new[] {
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
            CompareStatus(cacheManager, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task DeleteStorageFolderBlockWhileCopying()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            copyTCS.SetResult(); // We want the first ones to immediately proceed
            cacheManager.CopyFileCallback = async (string _, string _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);

            copyTCS = new TaskCompletionSource();
            var copyBlob2a = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2a");
            var copyBlob2b = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2b");
            var copyBlob2c = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2c");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);

            var copyBlob3 = cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
            var deleteStorageTask = cacheManager.DeleteStorageFolderAsync(folder1000Config.Path);

            await Task.Delay(50);

            // The deleting folder should have been able to delete 1 file but still waiting for the 2 other ones
            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (cacheManager.GetStorageFolderStatus()[0].CurrentSize != 110 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(cacheManager, new[] {
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
            CompareStatus(cacheManager, new StorageFolderStatus[] {});
        }

        [Test]
        public async Task UpdateStorageFolder()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            cacheManager.CopyFileCallback = (string _, string _) => Task.CompletedTask;

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 10, 10000);

            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 100, 10000);

            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 600, 10000);

            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 710, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            Guid fileBlob4 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob4, 5, 10000);

            await cacheManager.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 715, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            folder1000Config.MaximumSize = 610;
            cacheManager.UpdateStorageFolder(folder1000Config);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 605, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 610 }
            });

            folder1000Config.Path = GetNewStorageFolder();
            Assert.That(() => cacheManager.UpdateStorageFolder(folder1000Config), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task CacheFull()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            cacheManager.CopyFileCallback = async (string _, string _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 750, 10000);

            var copyBlob1 = cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            // Ask for a file that can only fit in cache if fileBlob1 is not in use (but it is)
            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 1000, 10000);

            var copyBlob2 = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Assert.That(async () => await copyBlob2, Throws.TypeOf<InvalidOperationException>());

            // Conclude fileBlob1
            copyTCS.SetResult();
            await copyBlob1;

            // And now we should be able to complete fileBlob2
            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");
        }

        [Test]
        public async Task UseUnreferenced()
        {
            var cacheManager = new CacheManager(_loggerStub);
            int fetchCount = 0;
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob, 750, 10000);

            await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(1));

            // Remove last reference to the blob
            cacheManager.DecreaseUsageCount(fileBlob);

            // While at it, to decrease reference yet another time, it should generate a warning message.
            Assert.That(_loggerStub.Messages, Is.Empty);
            cacheManager.DecreaseUsageCount(fileBlob);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(_loggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            _loggerStub.Messages.Clear();

            // As a result we should get an exception when asking to copy that blob
            Assert.That(async () => await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB"),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(1));

            // Try to increase usage count (but with the wrong size, should fail since a file blob should always have
            // the same size).
            Assert.That(() => cacheManager.IncreaseUsageCount(fileBlob, 751, 10000),
                        Throws.TypeOf<ArgumentException>());
            Assert.That(() => cacheManager.IncreaseUsageCount(fileBlob, 750, 10001),
                        Throws.TypeOf<ArgumentException>());

            // But should succeed if sizes matches
            cacheManager.IncreaseUsageCount(fileBlob, 750, 10000);

            // And now copying should work
            await cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");
            Assert.That(fetchCount, Is.EqualTo(1));
            Assert.That(copyCount, Is.EqualTo(2));
        }
        
        [Test]
        public async Task DecreaseUsageOfInUse()
        {
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    File.WriteAllText(cachePath, "Some content");
                    return Task.CompletedTask;
                };
            var copyTCS = new TaskCompletionSource();
            cacheManager.CopyFileCallback = async (string _, string _) =>
                {
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob, 10, 10000);

            var copyBlobA = cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobA");
            var copyBlobB = cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobB");
            var copyBlobC = cacheManager.CopyFileToAsync(fileBlob, "C:\\Temp\\BlobC");

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Decrease usage while in use, should generate a warning and postpone work until not in use anymore
            Assert.That(_loggerStub.Messages, Is.Empty);
            cacheManager.DecreaseUsageCount(fileBlob);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(_loggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_loggerStub.Messages[0].Content.Contains(fileBlob.ToString()), Is.True);
            _loggerStub.Messages.Clear();

            await Task.Delay(50);

            CompareStatus(cacheManager, new[] {
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
            while (cacheManager.GetStorageFolderStatus()[0].UnreferencedSize != 10 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 10, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }
        
        [Test]
        public async Task SaveAndLoadStorageFolder()
        {
            var cacheManager = new CacheManager(_loggerStub);
            int fetchCount = 0;
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            cacheManager.DecreaseUsageCount(fileBlob2);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 10,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            cacheManager.PersistStorageFolderStates();

            // Rebuild a new CacheManager from that saved content
            var oldCacheManager = cacheManager;
            cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = oldCacheManager.FetchFileCallback;
            cacheManager.CopyFileCallback = oldCacheManager.CopyFileCallback;
            oldCacheManager = null;

            // Reference 2 and 3 instead of 1 and 3 to test that list of referenced and non referenced files gets
            // updated both ways.
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            folder1000Config.MaximumSize *= 2;
            cacheManager.AddStorageFolder(folder1000Config);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 1,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Test that we can copy files after reload.
            var copyTasks = new List<Task>();
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(6));
        }

        [Test]
        public async Task SaveAndLoadStorageFolderWhileInUse()
        {
            var cacheManager = new CacheManager(_loggerStub);
            int fetchCount = 0;
            TaskCompletionSource fetchTCS = new();
            fetchTCS.SetResult();
            cacheManager.FetchFileCallback = async (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    await fetchTCS.Task;
                };
            int copyCount = 0;
            TaskCompletionSource copyTCS = new();
            copyTCS.SetResult();
            cacheManager.CopyFileCallback = async (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    await copyTCS.Task;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            copyTCS = new();
            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            var blob2Task = cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            var maxWaitTimer = new Stopwatch();
            maxWaitTimer.Start();
            while (copyCount != 2 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(copyCount, Is.EqualTo(2));

            fetchTCS = new();
            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            var blob3Task = cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            maxWaitTimer.Start();
            while (fetchCount != 3 && maxWaitTimer.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(2));

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            cacheManager.PersistStorageFolderStates();

            // Unblock everything (the goal was anyway to save while in use, so this is done)
            copyTCS.SetResult();
            fetchTCS.SetResult();
            await blob2Task;
            await blob3Task;

            // Rebuild a new CacheManager from that saved content
            var oldCacheManager = cacheManager;
            cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = oldCacheManager.FetchFileCallback;
            cacheManager.CopyFileCallback = oldCacheManager.CopyFileCallback;
            oldCacheManager = null;

            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            cacheManager.AddStorageFolder(folder1000Config);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Test that we can copy files after reload.
            var copyTasks = new List<Task>();
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(6));
        }

        [Test]
        public async Task FileInTwoStoragesOnReload()
        {
            // Prepare first CacheManager with 2 files
            var cacheManager1 = new CacheManager(_loggerStub);
            int fetchCount = 0;
            cacheManager1.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            cacheManager1.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folderAConfig = new StorageFolderConfig();
            folderAConfig.Path = GetNewStorageFolder();
            folderAConfig.MaximumSize = 2000;
            cacheManager1.AddStorageFolder(folderAConfig);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager1.IncreaseUsageCount(fileBlob1, 1, 10000);
            await cacheManager1.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager1.IncreaseUsageCount(fileBlob2, 10, 10000);
            await cacheManager1.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager1.IncreaseUsageCount(fileBlob3, 100, 10000);
            await cacheManager1.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(cacheManager1, new[] {
                new StorageFolderStatus() { Path = folderAConfig.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Save
            cacheManager1.PersistStorageFolderStates();

            // Prepare second CacheManager with again 2 files (one is the same as in the first one)
            var cacheManager2 = new CacheManager(_loggerStub);
            cacheManager2.FetchFileCallback = cacheManager1.FetchFileCallback;
            cacheManager2.CopyFileCallback = cacheManager1.CopyFileCallback;

            var folderBConfig = new StorageFolderConfig();
            folderBConfig.Path = GetNewStorageFolder();
            folderBConfig.MaximumSize = 2000;
            cacheManager2.AddStorageFolder(folderBConfig);

            cacheManager2.IncreaseUsageCount(fileBlob2, 10, 10000);
            await cacheManager2.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            cacheManager2.IncreaseUsageCount(fileBlob3, 100, 10000);
            await cacheManager2.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Guid fileBlob4 = Guid.NewGuid();
            cacheManager2.IncreaseUsageCount(fileBlob4, 1000, 10000);
            await cacheManager2.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");

            Assert.That(fetchCount, Is.EqualTo(6));
            Assert.That(copyCount, Is.EqualTo(6));

            CompareStatus(cacheManager2, new[] {
                new StorageFolderStatus() { Path = folderBConfig.Path, CurrentSize = 1110, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });

            // Save
            cacheManager2.PersistStorageFolderStates();

            // Create a new CacheManager using both storage folders
            var cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = cacheManager1.FetchFileCallback;
            cacheManager.CopyFileCallback = cacheManager1.CopyFileCallback;

            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            cacheManager.IncreaseUsageCount(fileBlob4, 1000, 10000);

            cacheManager.AddStorageFolder(folderAConfig);
            cacheManager.DecreaseUsageCount(fileBlob3);
            Assert.That(_loggerStub.Messages, Is.Empty);
            cacheManager.AddStorageFolder(folderBConfig);
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(2));
            Assert.That(_loggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_loggerStub.Messages[0].Content.Contains(fileBlob2.ToString()), Is.True);
            Assert.That(_loggerStub.Messages[0].Content.Contains("found in both"), Is.True);
            Assert.That(_loggerStub.Messages[1].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_loggerStub.Messages[1].Content.Contains(fileBlob3.ToString()), Is.True);
            Assert.That(_loggerStub.Messages[1].Content.Contains("found in both"), Is.True);
            _loggerStub.Messages.Clear();

            var copyTasks = new List<Task>();
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2") );
            copyTasks.Add( cacheManager.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4") );
            await Task.WhenAll( copyTasks );
            Assert.That(fetchCount, Is.EqualTo(6));
            Assert.That(copyCount, Is.EqualTo(9));

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folderAConfig.Path, CurrentSize = 111, UnreferencedSize = 100,
                                            ZombiesSize = 0, MaximumSize = 2000 },
                new StorageFolderStatus() { Path = folderBConfig.Path, CurrentSize = 1000, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 2000 }
            });
        }

        [Test]
        public async Task ReloadInSmaller()
        {
            var cacheManager = new CacheManager(_loggerStub);
            int fetchCount = 0;
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 111, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Save
            cacheManager.PersistStorageFolderStates();

            // Rebuild a new CacheManager from that saved content
            var oldCacheManager = cacheManager;
            cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = oldCacheManager.FetchFileCallback;
            cacheManager.CopyFileCallback = oldCacheManager.CopyFileCallback;
            oldCacheManager = null;

            // Reference 2 and 3 instead of 1 and 3 to test that list of referenced and non referenced files gets
            // updated both ways.
            cacheManager.IncreaseUsageCount(fileBlob1, 1, 10000);
            cacheManager.IncreaseUsageCount(fileBlob2, 10, 10000);
            cacheManager.IncreaseUsageCount(fileBlob3, 100, 10000);
            folder1000Config.MaximumSize = 101;
            cacheManager.AddStorageFolder(folder1000Config);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 100, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 101 }
            });
        }
        
        [Test]
        public async Task DealingWithZombies()
        {
            // Create a cache with some content
            var cacheManager = new CacheManager(_loggerStub);
            int fetchCount = 0;
            List<string> fetchedFiles = new();
            cacheManager.FetchFileCallback = (Guid _, string cachePath) =>
                {
                    Interlocked.Increment(ref fetchCount);
                    StringBuilder builder = new();
                    File.WriteAllText(cachePath, "Some content");
                    fetchedFiles.Add(cachePath);
                    return Task.CompletedTask;
                };
            int copyCount = 0;
            cacheManager.CopyFileCallback = (string _, string _) =>
                {
                    Interlocked.Increment(ref copyCount);
                    return Task.CompletedTask;
                };

            var folder1000Config = new StorageFolderConfig();
            folder1000Config.Path = GetNewStorageFolder();
            folder1000Config.MaximumSize = 1000;
            cacheManager.AddStorageFolder(folder1000Config);

            Guid fileBlob1 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob1, 100, 10000);
            await cacheManager.CopyFileToAsync(fileBlob1, "C:\\Temp\\Blob1");

            Guid fileBlob2 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob2, 300, 10000);
            await cacheManager.CopyFileToAsync(fileBlob2, "C:\\Temp\\Blob2");

            Guid fileBlob3 = Guid.NewGuid();
            cacheManager.IncreaseUsageCount(fileBlob3, 500, 10000);
            await cacheManager.CopyFileToAsync(fileBlob3, "C:\\Temp\\Blob3");

            Assert.That(fetchCount, Is.EqualTo(3));
            Assert.That(fetchedFiles.Count, Is.EqualTo(3));
            Assert.That(fetchedFiles[1].EndsWith(fileBlob2.ToString()), Is.True);
            Assert.That(copyCount, Is.EqualTo(3));

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 900, UnreferencedSize = 0,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });

            // Lock a file so that it cannot be deleted
            Assert.That(_loggerStub.Messages, Is.Empty);
            Guid fileBlob4 = Guid.NewGuid();
            using (var openedFile = File.Open(fetchedFiles[1], FileMode.Open))
            {
                // Ask for some content that will trigger cache eviction -> delete -> causing a zombie from the locked file.
                cacheManager.IncreaseUsageCount(fileBlob4, 400, 10000);
                await cacheManager.CopyFileToAsync(fileBlob4, "C:\\Temp\\Blob4");
            }
            Assert.That(_loggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(_loggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_loggerStub.Messages[0].Content.Contains(fileBlob2.ToString()), Is.True);
            _loggerStub.Messages.Clear();

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 912, UnreferencedSize = 0,
                                            ZombiesSize = 12, MaximumSize = 1000 }
            });

            // Save
            cacheManager.PersistStorageFolderStates();

            // Rebuild a new CacheManager from that saved content (will now get rid of zombies since the file is not
            // locked anymore).
            var oldCacheManager = cacheManager;
            cacheManager = new CacheManager(_loggerStub);
            cacheManager.FetchFileCallback = oldCacheManager.FetchFileCallback;
            cacheManager.CopyFileCallback = oldCacheManager.CopyFileCallback;
            oldCacheManager = null;
            cacheManager.AddStorageFolder(folder1000Config);

            CompareStatus(cacheManager, new[] {
                new StorageFolderStatus() { Path = folder1000Config.Path, CurrentSize = 900, UnreferencedSize = 900,
                                            ZombiesSize = 0, MaximumSize = 1000 }
            });
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                folderPath = folderPath.ToLower();
            }
            _storageFolders.Add(folderPath);
            return folderPath;
        }

        void CompareStatus(CacheManager manager, StorageFolderStatus[] expectedStatuses)
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

        List<string> _storageFolders = new();
        LoggerStub _loggerStub = new();
    }
}
