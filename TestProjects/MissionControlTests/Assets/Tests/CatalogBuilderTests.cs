using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    public class CatalogBuilderTests
    {
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

            m_StorageFolders.Clear();
        }

        [Test]
        public void MainUseCase()
        {
            string testFolder = GetNewStorageFolder();

            string capcomMainFileMd5 = GenerateTestFile(testFolder, "folder1/capcom.exe", 100);
            string sharedFile1Md5 = GenerateTestFile(testFolder, "folder1/shared1.dll", 100);
            string sharedFile2Md5 = GenerateTestFile(testFolder, "folder1/shared2.dll", 100);
            string otherDllMd5 = GenerateTestFile(testFolder, "folder1/other.dll", 100);
            string clusterDisplayMainExeMd5 = GenerateTestFile(testFolder, "main.exe", 100);

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "ClusterDisplay Capcom",
                        Type = "capcom",
                        LaunchPath = "folder1/capcom.exe"
                    },
                    ExclusiveFiles = new[] {"folder1/capcom.exe"},
                    Files = new[] {"folder1/*.dll"},
                    ExcludedFiles = new[] {"folder1/o*.dll"}
                },
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "My Project!",
                        Type = Launchable.ClusterNodeType,
                        LaunchPath = "main.exe"
                    },
                    Files = new[] {"*"},
                }
            };

            var catalog = CatalogBuilder.Build(testFolder, directives);
            Assert.That(catalog.Payloads.Count, Is.EqualTo(3));

            var payload = catalog.Payloads[0];
            Assert.That(payload.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "folder1/capcom.exe", capcomMainFileMd5);

            payload = catalog.Payloads[1];
            Assert.That(payload.Name, Is.EqualTo(directives[1].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(2));
            TestFile(payload.Files[0], "folder1/other.dll", otherDllMd5);
            TestFile(payload.Files[1], "main.exe", clusterDisplayMainExeMd5);

            payload = catalog.Payloads[2];
            Assert.That(payload.Name, Is.EqualTo("shared"));
            Assert.That(payload.Files.Count, Is.EqualTo(2));
            TestFile(payload.Files[0], "folder1/shared1.dll", sharedFile1Md5);
            TestFile(payload.Files[1], "folder1/shared2.dll", sharedFile2Md5);

            Assert.That(catalog.Launchables.Count, Is.EqualTo(2));

            var launchable = catalog.Launchables[0];
            Assert.That(launchable.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(2));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
            Assert.That(launchable.Payloads[1], Is.EqualTo("shared"));

            launchable = catalog.Launchables[1];
            Assert.That(launchable.Name, Is.EqualTo(directives[1].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(2));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
            Assert.That(launchable.Payloads[1], Is.EqualTo("shared"));
        }

        [Test]
        public void MultipleShared()
        {
            string testFolder = GetNewStorageFolder();

            string launchable1Md5 = GenerateTestFile(testFolder, "launchable1.exe", 100);
            string launchable2Md5 = GenerateTestFile(testFolder, "launchable2.exe", 100);
            string launchable3Md5 = GenerateTestFile(testFolder, "launchable3.exe", 100);
            string shared12Md5 = GenerateTestFile(testFolder, "shared/shared12.dll", 100);
            string shared13Md5 = GenerateTestFile(testFolder, "shared/shared13.dll", 100);
            string shared23Md5 = GenerateTestFile(testFolder, "shared/shared23.dll", 100);

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "launchable1",
                        Type = "test",
                        LaunchPath = "launchable1.exe"
                    },
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                },
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "launchable2",
                        Type = "test",
                        LaunchPath = "launchable2.exe"
                    },
                    Files = new[] {"launchable2.exe", "shared/shared12.dll", "shared/shared23.dll"}
                },
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "launchable3",
                        Type = "test",
                        LaunchPath = "launchable3.exe"
                    },
                    Files = new[] {"launchable3.exe", "shared/shared13.dll", "shared/shared23.dll"}
                }
            };

            var catalog = CatalogBuilder.Build(testFolder, directives);
            Assert.That(catalog.Payloads.Count, Is.EqualTo(6));

            var payload = catalog.Payloads[0];
            Assert.That(payload.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "launchable1.exe", launchable1Md5);

            payload = catalog.Payloads[1];
            Assert.That(payload.Name, Is.EqualTo(directives[1].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "launchable2.exe", launchable2Md5);

            payload = catalog.Payloads[2];
            Assert.That(payload.Name, Is.EqualTo(directives[2].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "launchable3.exe", launchable3Md5);

            payload = catalog.Payloads[3];
            Assert.That(payload.Name, Is.EqualTo("shared0"));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "shared/shared12.dll", shared12Md5);

            payload = catalog.Payloads[4];
            Assert.That(payload.Name, Is.EqualTo("shared1"));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "shared/shared13.dll", shared13Md5);

            payload = catalog.Payloads[5];
            Assert.That(payload.Name, Is.EqualTo("shared2"));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "shared/shared23.dll", shared23Md5);

            Assert.That(catalog.Launchables.Count, Is.EqualTo(3));

            var launchable = catalog.Launchables[0];
            Assert.That(launchable.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(3));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
            Assert.That(launchable.Payloads[1], Is.EqualTo("shared0"));
            Assert.That(launchable.Payloads[2], Is.EqualTo("shared1"));

            launchable = catalog.Launchables[1];
            Assert.That(launchable.Name, Is.EqualTo(directives[1].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(3));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
            Assert.That(launchable.Payloads[1], Is.EqualTo("shared0"));
            Assert.That(launchable.Payloads[2], Is.EqualTo("shared2"));

            launchable = catalog.Launchables[2];
            Assert.That(launchable.Name, Is.EqualTo(directives[2].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(3));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
            Assert.That(launchable.Payloads[1], Is.EqualTo("shared1"));
            Assert.That(launchable.Payloads[2], Is.EqualTo("shared2"));
        }

        [Test]
        public void NullLaunchable()
        {
            string testFolder = GetNewStorageFolder();

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                }
            };

            Assert.That(() => CatalogBuilder.Build(testFolder, directives),
                Throws.TypeOf<ArgumentException>().With.Message.StartsWith("Every directive need to have a Launchable."));
        }

        [Test]
        public void SameNameLaunchable()
        {
            string testFolder = GetNewStorageFolder();

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "launchableName",
                        Type = "test",
                        LaunchPath = "launchable1.exe"
                    },
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                },
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "launchableName",
                        Type = "test",
                        LaunchPath = "launchable2.exe"
                    },
                    Files = new[] {"launchable2.exe", "shared/shared12.dll", "shared/shared23.dll"}
                }
            };

            Assert.That(() => CatalogBuilder.Build(testFolder, directives),
                Throws.TypeOf<ArgumentException>().With.Message.StartsWith("Some launchable share the same name, " +
                    "every launchable must have a unique name within the LaunchCatalog."));
        }

        [Test]
        public void EmptyLaunchableName()
        {
            string testFolder = GetNewStorageFolder();

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Type = "test",
                        LaunchPath = "launchable1.exe"
                    },
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                }
            };

            Assert.That(() => CatalogBuilder.Build(testFolder, directives),
                Throws.TypeOf<ArgumentException>().With.Message.StartsWith("Launchable name cannot be empty."));
        }

        [Test]
        public void EmptyLaunchableType()
        {
            string testFolder = GetNewStorageFolder();

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "TestName",
                        LaunchPath = "launchable1.exe"
                    },
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                }
            };

            Assert.That(() => CatalogBuilder.Build(testFolder, directives),
                Throws.TypeOf<ArgumentException>().With.Message.StartsWith("Launchable type cannot be empty."));
        }

        [Test]
        public void EmptyLaunchPath()
        {
            string testFolder = GetNewStorageFolder();

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "TestName",
                        Type = "test"
                    },
                    Files = new[] {"launchable1.exe", "shared/shared12.dll", "shared/shared13.dll"}
                }
            };

            Assert.That(() => CatalogBuilder.Build(testFolder, directives),
                Throws.TypeOf<ArgumentException>().With.Message.StartsWith("Launchable launchPath cannot be empty."));
        }

        [Test]
        public void BigFileChecksum()
        {
            string testFolder = GetNewStorageFolder();

            string bigFileMd5 = GenerateTestFile(testFolder, "bigfile.bin", 10 * 1024 * 1024);

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "Some name",
                        Type = "test",
                        LaunchPath = "bigfile.bin"
                    },
                    Files = new[] {"bigfile.bin"}
                }
            };

            var catalog = CatalogBuilder.Build(testFolder, directives);
            Assert.That(catalog.Payloads.Count, Is.EqualTo(1));

            var payload = catalog.Payloads[0];
            Assert.That(payload.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(1));
            TestFile(payload.Files[0], "bigfile.bin", bigFileMd5);

            Assert.That(catalog.Launchables.Count, Is.EqualTo(1));

            var launchable = catalog.Launchables[0];
            Assert.That(launchable.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(1));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
        }

        [Test]
        public void DropsLaunchableWithoutFiles()
        {
            string testFolder = GetNewStorageFolder();

            string capcomMainFileMd5 = GenerateTestFile(testFolder, "folder1/capcom.exe", 100);
            string sharedFile1Md5 = GenerateTestFile(testFolder, "folder1/shared1.dll", 100);
            string sharedFile2Md5 = GenerateTestFile(testFolder, "folder1/shared2.dll", 100);
            GenerateTestFile(testFolder, "folder1/other.dll", 100);
            GenerateTestFile(testFolder, "main.exe", 100);

            var directives = new[]
            {
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "ClusterDisplay Capcom",
                        Type = "capcom",
                        LaunchPath = "folder1/capcom.exe"
                    },
                    ExclusiveFiles = new[] {"folder1/capcom.exe"},
                    Files = new[] {"folder1/*.dll"},
                    ExcludedFiles = new[] {"folder1/o*.dll"}
                },
                new CatalogBuilderDirective()
                {
                    Launchable = new()
                    {
                        Name = "My Project!",
                        Type = Launchable.ClusterNodeType,
                        LaunchPath = "main.exe"
                    },
                    Files = new[] {"*"},
                    ExcludedFiles = new[] {"*.dll", "*.exe"}
                }
            };

            var catalog = CatalogBuilder.Build(testFolder, directives);
            Assert.That(catalog.Payloads.Count, Is.EqualTo(1));

            var payload = catalog.Payloads[0];
            Assert.That(payload.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(payload.Files.Count, Is.EqualTo(3));
            TestFile(payload.Files[0], "folder1/capcom.exe", capcomMainFileMd5);
            TestFile(payload.Files[1], "folder1/shared1.dll", sharedFile1Md5);
            TestFile(payload.Files[2], "folder1/shared2.dll", sharedFile2Md5);

            Assert.That(catalog.Launchables.Count, Is.EqualTo(1));

            var launchable = catalog.Launchables[0];
            Assert.That(launchable.Name, Is.EqualTo(directives[0].Launchable.Name));
            Assert.That(launchable.Payloads.Count, Is.EqualTo(1));
            Assert.That(launchable.Payloads[0], Is.EqualTo(launchable.Name));
        }

        static void TestFile(PayloadFile payloadFile, string expectedPath, string expectedMd5)
        {
            Assert.That(payloadFile.Path, Is.EqualTo(expectedPath));
            Assert.That(payloadFile.Md5, Is.EqualTo(expectedMd5));
        }

        static byte[] GetRandomBytes(int size)
        {
            byte[] ret = new byte[size];
            for (int currentPos = 0; currentPos < size;)
            {
                const int bytesInGuid = 16;
                int copyLength = Math.Min(size - currentPos, bytesInGuid);
                byte[] guidBytes = Guid.NewGuid().ToByteArray();
                Span<byte> guidSpan = new(guidBytes, 0, copyLength);
                guidSpan.CopyTo(new Span<byte>(ret, currentPos, copyLength));
                currentPos += copyLength;
            }

            return ret;
        }

        static string ComputeMd5String(byte[] bytes)
        {
            using var md5 = MD5.Create();
            var bytesHash = md5.ComputeHash(bytes);
            return ConvertHelpers.ToHexString(bytesHash);
        }

        static string GenerateTestFile(string basePath, string fileRelativePath, int length)
        {
            byte[] bytes = GetRandomBytes(length);
            string fullPath = Path.Combine(basePath, fileRelativePath);
            string directory = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, bytes);
            return ComputeMd5String(bytes);
        }

        string GetNewStorageFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "CatalogBuilderTests" + Guid.NewGuid().ToString());
            m_StorageFolders.Add(folderPath);
            return folderPath;
        }

        List<string> m_StorageFolders = new();
    }
}
