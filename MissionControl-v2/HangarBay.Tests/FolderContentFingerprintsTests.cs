using Microsoft.Extensions.Logging;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class FolderContentFingerprintsTests
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
        public void CleanupNewFiles()
        {
            var folder = GetNewStorageFolder();
            AddFiles(folder, folderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder);

            AddFiles(folder, new[] {"File3", "Folder1/File4", "Folder1/FolderA/FileA3","Folder1/FolderB/Folder$/File"});
            timestamps.CleanModified(folder, m_LoggerStub);

            Assert.That(TestFolderFiles(folder, folderLayout1), Is.True);
        }

        [Test]
        public void CleanupModifedFiles()
        {
            var folder = GetNewStorageFolder();
            AddFiles(folder, folderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder);

            TouchFiles(folder, new[] { "File2", "Folder1/File2", "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2",
                "Folder2/File1", "Folder2/File2", "Folder2/File3","Folder2/FolderA/FileA1", "Folder2/FolderA/FileA2",
                "Folder2/FolderB/FileB1", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"});
            timestamps.CleanModified(folder, m_LoggerStub);
            Assert.That(Directory.Exists(Path.Combine(folder, "Folder2")), Is.False);

            Assert.That(TestFolderFiles(folder, new[]{ "File1", "Folder1/File1", "Folder1/File3", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder1/FolderB/FileB3",}), Is.True);
        }

        [Test]
        public void CleanupEverything()
        {
            var folder = GetNewStorageFolder();
            AddFiles(folder, folderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder);

            TouchFiles(folder, folderLayout1);
            timestamps.CleanModified(folder, m_LoggerStub);

            Assert.That(Directory.Exists(folder), Is.True);
            Assert.That(Directory.EnumerateFileSystemEntries(folder).Any(), Is.False);
        }

        [Test]
        public void CleanupModifedFilesFromLoadedFingerprints()
        {
            var folder = GetNewStorageFolder();
            AddFiles(folder, folderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder);

            string fingerprintsFile = Path.Combine(folder, "fingerprints.json");
            timestamps.SaveTo(fingerprintsFile);
            timestamps = FolderContentFingerprints.LoadFrom(fingerprintsFile);

            TouchFiles(folder, new[] { "File2", "Folder1/File2", "Folder1/FolderA/FileA2", "Folder1/FolderB/FileB2",
                "Folder2/File1", "Folder2/File2", "Folder2/File3","Folder2/FolderA/FileA1", "Folder2/FolderA/FileA2",
                "Folder2/FolderB/FileB1", "Folder2/FolderB/FileB2", "Folder2/FolderB/FileB3"});
            timestamps.CleanModified(folder, m_LoggerStub);
            Assert.That(Directory.Exists(Path.Combine(folder, "Folder2")), Is.False);

            Assert.That(TestFolderFiles(folder, new[]{ "File1", "Folder1/File1", "Folder1/File3", "Folder1/FolderA/FileA1",
                "Folder1/FolderB/FileB1", "Folder1/FolderB/FileB3",}), Is.True);
        }

        [Test]
        public void CleanupWithFileInUse()
        {
            var folder = GetNewStorageFolder();
            AddFiles(folder, folderLayout1);
            var timestamps = FolderContentFingerprints.BuildFrom(folder);

            TouchFiles(folder, folderLayout1);
            using var fileInUse = File.Open(Path.Combine(folder, "Folder2/FolderA/FileA1"), FileMode.Open);

            Assert.That(() => timestamps.CleanModified(folder, m_LoggerStub), Throws.TypeOf<IOException>());
            Assert.That(m_LoggerStub.Messages.Count, Is.EqualTo(1));
            Assert.That(m_LoggerStub.Messages[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(m_LoggerStub.Messages[0].Content.Contains("FileA1"), Is.True);
            m_LoggerStub.Messages.Clear();
        }

        static readonly string[] folderLayout1 = new[] {
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

        void AddFiles(string folder, IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                AddFile(folder, filePath);
            }
        }

        void AddFile(string folder, string filePath)
        {
            var fullPath = Path.Combine(folder, filePath);
            var directories = Path.GetDirectoryName(fullPath);
            if (directories != null)
            {
                Directory.CreateDirectory(directories);
            }
            File.WriteAllText(fullPath, "Some content");
        }

        bool TestFolderFiles(string folder, IEnumerable<string> filePaths)
        {
            var filePathsSet = new HashSet<string>(filePaths.Select(fp => fp.Replace("\\",$"{Path.DirectorySeparatorChar}").Replace("/",$"{Path.DirectorySeparatorChar}")));

            var enumOptions = new EnumerationOptions();
            enumOptions.RecurseSubdirectories = true;
            var folderFiles = Directory.GetFiles(folder, "*", enumOptions);

            foreach(var currentFile in folderFiles)
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

        void TouchFiles(string folder, IEnumerable<string> filePaths)
        {
            foreach(var currentFile in filePaths)
            {
                var fullPath = Path.Combine(folder, currentFile);
                File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
            }
        }

        List<string> m_StorageFolders = new();
        LoggerStub m_LoggerStub = new();
    }
}
