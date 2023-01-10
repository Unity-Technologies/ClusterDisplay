using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;
using Unity.ClusterDisplay.MissionControl.MissionControl;
// ReSharper disable StructuredMessageTemplateProblem

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    public class FolderContentFingerprintsTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
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
        public void CleanupNewFiles()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            CreateFiles(folder, new[] {"File3", "Folder1/File4", "Folder1/FolderA/FileA3","Folder1/FolderB/Folder$/File"});
            timestamps.PrepareForPayload(folder, folderPayload, m_LoggerMock.Object);

            Assert.That(TestFolderFiles(folder, k_FolderLayout1), Is.True);
        }

        [Test]
        public void CleanupModifiedFiles()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            TouchFiles(folder, new[] { "File2", "Folder1/File2", "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2",
                "Folder2/File1", "Folder2/File2", "Folder2/File3","Folder2/FolderA/FileA1", "Folder2/FolderA/FileA2",
                "Folder2/FolderB/FileB1", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"});
            timestamps.PrepareForPayload(folder, folderPayload, m_LoggerMock.Object);
            Assert.That(Directory.Exists(Path.Combine(folder, "Folder2")), Is.False);

            Assert.That(TestFolderFiles(folder, new[]{ "File1", "Folder1/File1", "Folder1/File3", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder1/FolderB/FileB3",}), Is.True);
        }

        [Test]
        public void CleanupDifferentBlobFiles()
        {
            var folder = GetNewStorageFolder();
            var payload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, payload);

            payload = ChangeExpectedBlobIds(payload, new[] { "File1", "Folder1/File1", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder2/File1", "Folder2/FolderA/FileA1", "Folder2/FolderB/FileB1" });
            timestamps.PrepareForPayload(folder, payload, m_LoggerMock.Object);

            Assert.That(TestFolderFiles(folder, new[]{ "File2", "Folder1/File2", "Folder1/File3",
                "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2", "Folder1/FolderB/FileB3", "Folder2/File2",
                "Folder2/File3", "Folder2/FolderA/FileA2", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"}),
                Is.True);
        }

        [Test]
        public void CleanupUnnecessaryFiles()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            var newPayload = RemoveFilesFromPayload(folderPayload, new[] { "Folder1/File3", "Folder1/FolderB/FileB3",
                "Folder2/File3", "Folder2/FolderB/FileB3" });

            timestamps.PrepareForPayload(folder, folderPayload, m_LoggerMock.Object);
            Assert.That(TestFolderFiles(folder, k_FolderLayout1));

            timestamps.PrepareForPayload(folder, newPayload, m_LoggerMock.Object);
            Assert.That(TestFolderFiles(folder, new[]{ "File1", "File2", "Folder1/File1", "Folder1/File2",
                "Folder1/FolderA/FileA1", "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB1", "Folder1/FolderB/FileB2",
                "Folder2/File1", "Folder2/File2", "Folder2/FolderA/FileA1", "Folder2/FolderA/FileA2",
                "Folder2/FolderB/FileB1", "Folder2/FolderB/FileB2"}),
                Is.True);
        }

        [Test]
        public void CleanupEverything()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            TouchFiles(folder, k_FolderLayout1);
            timestamps.PrepareForPayload(folder, folderPayload, m_LoggerMock.Object);

            Assert.That(Directory.Exists(folder), Is.True);
            Assert.That(Directory.EnumerateFileSystemEntries(folder).Any(), Is.False);
        }

        [Test]
        public void CleanupModifiedFilesFromLoadedFingerprints()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            string fingerprintsFile = Path.Combine(folder, "fingerprints.json");
            timestamps.SaveTo(fingerprintsFile);
            timestamps = FolderContentFingerprints.LoadFrom(fingerprintsFile);

            TouchFiles(folder, new[] { "File2", "Folder1/File2", "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2",
                "Folder2/File1", "Folder2/File2", "Folder2/File3","Folder2/FolderA/FileA1", "Folder2/FolderA/FileA2",
                "Folder2/FolderB/FileB1", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"});
            timestamps.PrepareForPayload(folder, folderPayload, m_LoggerMock.Object);
            Assert.That(Directory.Exists(Path.Combine(folder, "Folder2")), Is.False);

            Assert.That(TestFolderFiles(folder, new[]{ "File1", "Folder1/File1", "Folder1/File3", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder1/FolderB/FileB3",}), Is.True);
        }

        [Test]
        public void CleanupDifferentBlobFilesFromLoadedFingerprints()
        {
            var folder = GetNewStorageFolder();
            var payload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, payload);

            string fingerprintsFile = Path.Combine(folder, "fingerprints.json");
            timestamps.SaveTo(fingerprintsFile);
            timestamps = FolderContentFingerprints.LoadFrom(fingerprintsFile);

            payload = ChangeExpectedBlobIds(payload, new[] { "File1", "Folder1/File1", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder2/File1", "Folder2/FolderA/FileA1", "Folder2/FolderB/FileB1" });
            timestamps.PrepareForPayload(folder, payload, m_LoggerMock.Object);

            Assert.That(TestFolderFiles(folder, new[]{ "File2", "Folder1/File2", "Folder1/File3",
                "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2", "Folder1/FolderB/FileB3", "Folder2/File2",
                "Folder2/File3", "Folder2/FolderA/FileA2", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"}),
                Is.True);
        }

        [Test]
        public void CleanupWithFileInUseNotInFuturePayload()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            var filePath = Path.Combine(folder, "Folder2/FolderA/FileA1");
            using var fileInUse = File.Open(filePath, FileMode.Open);

            timestamps.PrepareForPayload(folder, new Payload(), m_LoggerMock.Object);
            m_LoggerMock.VerifyLog(l => l.LogWarning("Failed to delete*", Path.GetFullPath(filePath)));

            // Remarks, everything else should be gone
            Assert.That(TestFolderFiles(folder, new[]{ "Folder2/FolderA/FileA1" }), Is.True);
        }

        [Test]
        public void CleanupWithFileInUseInFuturePayload()
        {
            var folder = GetNewStorageFolder();
            var folderPayload = CreateFiles(folder, k_FolderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder, folderPayload);

            var filePath = Path.Combine(folder, "Folder2/FolderA/FileA1");
            using var fileInUse = File.Open(filePath, FileMode.Open);

            TouchFiles(folder, new[] { "Folder2/FolderA/FileA1" });
            var futurePayload = new Payload(folderPayload.Files.Where(pf => pf.Path == "Folder2/FolderA/FileA1"));

            Assert.That(() => timestamps.PrepareForPayload(folder, futurePayload, m_LoggerMock.Object),
                        Throws.TypeOf<IOException>());
            m_LoggerMock.VerifyLog(l => l.LogError(It.IsAny<IOException>(), "Failed to delete*", Path.GetFullPath(filePath)));
        }

        static readonly string[] k_FolderLayout1 = new[] {
            "File1",
            "File2",
            "Folder1/File1",
            "Folder1/File2",
            "Folder1/File3",
            "Folder1/FolderA/FileA1",
            "Folder1/FolderA/FileA2",
            "Folder1/FolderB/FileB1",
            "Folder1/FolderB/FileB2",
            "Folder1/FolderB/FileB3",
            "Folder2/File1",
            "Folder2/File2",
            "Folder2/File3",
            "Folder2/FolderA/FileA1",
            "Folder2/FolderA/FileA2",
            "Folder2/FolderB/FileB1",
            "Folder2/FolderB/FileB2",
            "Folder2/FolderB/FileB3"
        };

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "FolderContentFingerprintsTests_" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        static Payload CreateFiles(string folder, IEnumerable<string> filePaths)
        {
            var payloadFiles = new List<PayloadFile>();
            foreach (var filePath in filePaths)
            {
                var fullPath = Path.Combine(folder, filePath);
                var directories = Path.GetDirectoryName(fullPath);
                if (directories != null)
                {
                    Directory.CreateDirectory(directories);
                }
                File.WriteAllText(fullPath, "Some content");
                payloadFiles.Add(new PayloadFile(filePath, Guid.NewGuid(), 0, 0));
            }
            return new Payload(payloadFiles);
        }

        static bool TestFolderFiles(string folder, IEnumerable<string> filePaths)
        {
            var filePathsSet = new HashSet<string>(filePaths.Select(fp => fp.Replace("\\",$"{Path.DirectorySeparatorChar}").Replace("/",$"{Path.DirectorySeparatorChar}")));

            var enumOptions = new EnumerationOptions();
            enumOptions.RecurseSubdirectories = true;
            var folderFiles = Directory.GetFiles(folder, "*", enumOptions);

            foreach (var currentFile in folderFiles)
            {
                var currentRelativePath = Path.GetRelativePath(folder, currentFile);
                if (!filePathsSet.Remove(currentRelativePath))
                {
                    return false; // Folder contains a file it shouldn't
                }
            }

            // If false it is because there are files left in filePathsSet, so files are missing from the folder.
            return !filePathsSet.Any();
        }

        static void TouchFiles(string folder, IEnumerable<string> filePaths)
        {
            foreach (var currentFile in filePaths)
            {
                var fullPath = Path.Combine(folder, currentFile);
                File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
            }
        }

        static Payload ChangeExpectedBlobIds(Payload payload, IEnumerable<string> filePaths)
        {
            List<PayloadFile> newFiles = new();
            foreach (var currentPayload in payload.Files)
            {
                if (filePaths.Contains(currentPayload.Path))
                {
                    newFiles.Add(new(currentPayload.Path, Guid.NewGuid(), currentPayload.CompressedSize,
                        currentPayload.Size));
                }
                else
                {
                    newFiles.Add(currentPayload);
                }
            }
            return new Payload(newFiles);
        }

        static Payload RemoveFilesFromPayload(Payload payload, IEnumerable<string> filePaths)
        {
            var newList = new List<PayloadFile>();
            foreach (var file in payload.Files)
            {
                if (filePaths.FirstOrDefault(s => s == file.Path) == null)
                {
                    newList.Add(file);
                }
            }
            return new Payload(newList);
        }

        List<string> m_StorageFolders = new();
        Mock<ILogger> m_LoggerMock = new();
    }
}
