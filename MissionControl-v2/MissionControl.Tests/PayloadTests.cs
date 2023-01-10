using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
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

            Payload payload1 = new(new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("SomeFolder/../TestFile",  fileBlob1, 100, 200),
                new PayloadFile("A/B/c/d/Filename",        fileBlob3, 700, 900)
            });

            Payload payload2 = new(new[] {
                new PayloadFile("Somewhere/Else",          fileBlob1, 100, 200),
                new PayloadFile("YetAnotherFile",          fileBlob4, 500, 700),
                new PayloadFile("A/b/../../OtherFilename", fileBlob2, 300, 400),
                new PayloadFile("Simple/Filename",         fileBlob3, 700, 900)
            });

            var mergedPayload = Payload.Merge(new[]{payload1, payload2});
            Assert.That(ComparePayloadArray(mergedPayload.Files, new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("A/B/c/d/Filename",        fileBlob3, 700, 900),
                new PayloadFile("Somewhere/Else",          fileBlob1, 100, 200),
                new PayloadFile("YetAnotherFile",          fileBlob4, 500, 700),
                new PayloadFile("Simple/Filename",         fileBlob3, 700, 900)
            }));
        }

        [Test]
        public void TestInternalConflict()
        {
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();

            Payload payload = new(new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("SomeFolder/TestFile",     fileBlob1, 100, 200),
                new PayloadFile("A/b/../../OtherFilename", fileBlob3, 300, 400)
            });
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            var newFiles = payload.Files.ToList();
            newFiles[^1] = new(payload.Files.Last().Path, fileBlob2, 301, payload.Files.Last().Size);
            payload = new(newFiles);
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            newFiles = payload.Files.ToList();
            newFiles[^1] = new(payload.Files.Last().Path, payload.Files.Last().FileBlob, 300, 401);
            payload = new(newFiles);
            Assert.That(() => Payload.Merge(new[] { payload }), Throws.TypeOf<ArgumentException>());

            newFiles = payload.Files.ToList();
            newFiles[^1] = new(payload.Files.Last().Path, payload.Files.Last().FileBlob,
                payload.Files.Last().CompressedSize, 400);
            payload = new(newFiles);
            var merged = Payload.Merge(new[] { payload });
            Assert.That(ComparePayloadArray(merged.Files, new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("SomeFolder/TestFile",     fileBlob1, 100, 200),
            }));
        }

        [Test]
        public void TestInterConflict()
        {
            var fileBlob1 = Guid.NewGuid();
            var fileBlob2 = Guid.NewGuid();
            var fileBlob3 = Guid.NewGuid();
            var fileBlob4 = Guid.NewGuid();

            Payload payload1 = new(new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("SomeFolder/../TestFile",  fileBlob1, 100, 200),
                new PayloadFile("A/B/c/d/Filename",        fileBlob3, 700, 900)
            });

            List<PayloadFile> payload2Files = new(new[] {
                new PayloadFile("W/../TestFile",           fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("FolderSome/../TestFile",  fileBlob1, 100, 200),
                new PayloadFile("A/B/c/e/../d/Filename",   fileBlob3, 700, 900)
            });
            Payload payload2 = new(payload2Files);

            payload2Files[0] = new(payload2Files[0].Path, fileBlob4, payload2Files[0].CompressedSize, payload2Files[0].Size);
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2Files[0] = new(payload2Files[0].Path, fileBlob1, payload2Files[0].CompressedSize, payload2Files[0].Size);

            payload2Files[1] = new(payload2Files[1].Path, payload2Files[1].FileBlob, 301, payload2Files[1].Size);
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2Files[1] = new(payload2Files[1].Path, payload2Files[1].FileBlob, 300, payload2Files[1].Size);

            payload2Files[3] = new(payload2Files[3].Path, payload2Files[3].FileBlob, payload2Files[3].CompressedSize, 899);
            Assert.That(() => Payload.Merge(new[] { payload1, payload2 }), Throws.TypeOf<ArgumentException>());
            payload2Files[3] = new(payload2Files[3].Path, payload2Files[3].FileBlob, payload2Files[3].CompressedSize, 900);

            var mergedPayload = Payload.Merge(new[] { payload1, payload2 });
            Assert.That(ComparePayloadArray(mergedPayload.Files, new[] {
                new PayloadFile("TestFile",                fileBlob1, 100, 200),
                new PayloadFile("OtherFilename",           fileBlob2, 300, 400),
                new PayloadFile("A/B/c/d/Filename",        fileBlob3, 700, 900)
            }));
        }

        static bool ComparePayloadArray(IEnumerable<PayloadFile> list1, IEnumerable<PayloadFile> list2)
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
