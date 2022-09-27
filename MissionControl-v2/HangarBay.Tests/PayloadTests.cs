using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    public class PayloadTests
    {
        [Test]
        public void TestOk()
        {
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();
            var fileBlob4 = Guid.NewGuid();

            var payload1 = new Payload();
            payload1.Files = new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "SomeFolder/../TestFile",  FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "A/B/c/d/Filename",        FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            };

            var payload2 = new Payload();
            payload2.Files = new[] {
                new PayloadFile() { Path = "Somewhere/Else",          FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "YetAnotherFile",          FileBlob = fileBlob4, CompressedSize = 500, Size = 700 },
                new PayloadFile() { Path = "A/b/../../OtherFilename", FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "Simple/Filename",         FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            };

            var mergedPayload = Payload.Merge(new[]{payload1, payload2});
            Assert.That(ComparePayloadArray(mergedPayload.Files, new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "A/B/c/d/Filename",        FileBlob = fileBlob3, CompressedSize = 700, Size = 900 },
                new PayloadFile() { Path = "Somewhere/Else",          FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "YetAnotherFile",          FileBlob = fileBlob4, CompressedSize = 500, Size = 700 },
                new PayloadFile() { Path = "Simple/Filename",         FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            }));
        }

        [Test]
        public void TestInternalConflict()
        {
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();

            var payload = new Payload();
            payload.Files = new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "SomeFolder/TestFile",     FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "A/b/../../OtherFilename", FileBlob = fileBlob3, CompressedSize = 300, Size = 400 }
            };
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            payload.Files.Last().FileBlob = fileBlob2;
            payload.Files.Last().CompressedSize = 301;
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            payload.Files.Last().CompressedSize = 300;
            payload.Files.Last().Size = 401;
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            payload.Files.Last().Size = 400;
            var merged = Payload.Merge(new[] { payload });
            Assert.That(ComparePayloadArray(merged.Files, new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "SomeFolder/TestFile",     FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
            }));
        }

        [Test]
        public void TestInterConflict()
        {
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();
            var fileBlob4 = Guid.NewGuid();

            var payload1 = new Payload();
            payload1.Files = new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "SomeFolder/../TestFile",  FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "A/B/c/d/Filename",        FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            };

            var payload2 = new Payload();
            payload2.Files = new[] {
                new PayloadFile() { Path = "W/../TestFile",           FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "FolderSome/../TestFile",  FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "A/B/c/e/../d/Filename",   FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            };

            payload2.Files.ElementAt(0).FileBlob = fileBlob4;
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2.Files.ElementAt(0).FileBlob = fileBlob1;

            payload2.Files.ElementAt(1).CompressedSize = 301;
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2.Files.ElementAt(1).CompressedSize = 300;

            payload2.Files.ElementAt(3).Size = 899;
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2.Files.ElementAt(3).Size = 900;

            var mergedPayload = Payload.Merge(new[] { payload1, payload2 });
            Assert.That(ComparePayloadArray(mergedPayload.Files, new[] {
                new PayloadFile() { Path = "TestFile",                FileBlob = fileBlob1, CompressedSize = 100, Size = 200 },
                new PayloadFile() { Path = "OtherFilename",           FileBlob = fileBlob2, CompressedSize = 300, Size = 400 },
                new PayloadFile() { Path = "A/B/c/d/Filename",        FileBlob = fileBlob3, CompressedSize = 700, Size = 900 }
            }));
        }

        bool ComparePayloadArray(IEnumerable<PayloadFile> list1, IEnumerable<PayloadFile> list2)
        {
            var array1 = list1.ToArray();
            var array2 = list2.ToArray();
            if (array1.Length != array2.Length)
            {
                return false;
            }
            for (int i = 0; i < array1.Length; ++i)
            {
                if (!ComparePayload(array1[i], array2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        static bool ComparePayload(PayloadFile payloadFile1, PayloadFile payloadFile2)
        {
            return JsonSerializer.Serialize(payloadFile1) == JsonSerializer.Serialize(payloadFile2);
        }
    }
}
