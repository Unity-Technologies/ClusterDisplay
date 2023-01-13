using System;
using System.Security.Cryptography;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public static class Helpers
    {
        /// <summary>
        /// Gets the <see cref="IncrementalCollectionUpdate{T}"/> from the collections returned by
        /// incrementalCollectionsUpdate REST method.
        /// </summary>
        /// <typeparam name="T"><see cref="IIncrementalCollectionObject"/> of <paramref name="collectionName"/>.</typeparam>
        /// <param name="collectionsUpdate">All the incremental collections updates returned by
        /// incrementalCollectionsUpdate REST method.</param>
        /// <param name="collectionName">Name of the collection to get the <see cref="IncrementalCollectionUpdate{T}"/>
        /// for.</param>
        public static IncrementalCollectionUpdate<T> GetCollectionUpdate<T>(
            Dictionary<string, JsonElement> collectionsUpdate, string collectionName) where T: IIncrementalCollectionObject
        {
            Assert.That(collectionsUpdate.ContainsKey(collectionName), Is.True);
            var assetsUpdate =
                JsonSerializer.Deserialize<IncrementalCollectionUpdate<T>>(collectionsUpdate[collectionName],
                    Json.SerializerOptions);
            Assert.That(assetsUpdate, Is.Not.Null);
            return assetsUpdate!;
        }

        /// <summary>
        /// Create an asset with dummy content.
        /// </summary>
        /// <param name="folder">Folder in which to create the asset.</param>
        /// <param name="catalog">LaunchCatalog.json content.</param>
        /// <param name="filesLength">Length of the files referenced by the catalog.  Files not in this dictionary have
        /// to be in <paramref name="filesContent"/>.</param>
        /// <param name="filesContent">Content of the files referenced by the catalog.  Files not in this dictionary
        /// have to be in <paramref name="filesLength"/>.</param>
        /// <returns>The folder in which the asset was created.</returns>
        public static async Task<string> CreateAsset(string folder, LaunchCatalog.Catalog catalog,
            Dictionary<string, int> filesLength, Dictionary<string, MemoryStream>? filesContent = null)
        {
            Directory.CreateDirectory(folder);

            Dictionary<string, string> filesMd5 = new();
            foreach (var fileLength in filesLength)
            {
                var filePath = Path.Combine(folder, fileLength.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var fileBytes = GetRandomBytes(fileLength.Value);
                filesMd5[fileLength.Key] = ComputeMd5String(fileBytes);
                await File.WriteAllBytesAsync(filePath, fileBytes);
            }
            foreach (var fileContent in filesContent ?? new())
            {
                var filePath = Path.Combine(folder, fileContent.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                fileContent.Value.Position = 0;
                filesMd5[fileContent.Key] = ComputeMd5String(fileContent.Value);

                fileContent.Value.Position = 0;
                await using var writeStream = File.OpenWrite(filePath);
                await fileContent.Value.CopyToAsync(writeStream);
            }

            foreach (var payload in catalog.Payloads)
            {
                foreach (var file in payload.Files)
                {
                    file.Md5 = filesMd5[file.Path];
                }
            }

            await using var fileStream = File.OpenWrite(Path.Combine(folder, "LaunchCatalog.json"));
            fileStream.SetLength(0);
            await JsonSerializer.SerializeAsync(fileStream, catalog, Json.SerializerOptions);

            return folder;
        }

        /// <summary>
        /// Create an asset with dummy content in the given mission control process.
        /// </summary>
        /// <param name="processHelper">Mission control process.</param>
        /// <param name="tempFolder">Temp folder in which to create the asset files to import it in
        /// <paramref name="processHelper"/>.</param>
        /// <param name="catalog">Catalog of the asset.</param>
        /// <param name="filesLength">Size of the files in <paramref name="catalog"/>.</param>
        /// <param name="filesContent">Content of the files referenced by the catalog.  Files not in this dictionary
        /// have to be in <paramref name="filesLength"/>.</param>
        /// <param name="name">Name of the <see cref="Asset"/>.</param>
        /// <param name="description">Description of the <see cref="Asset"/>.</param>
        /// <returns>Identifier of the created <see cref="Asset"/>.</returns>
        internal static async Task<Guid> PostAsset(MissionControlProcessHelper processHelper, string tempFolder,
            LaunchCatalog.Catalog catalog, Dictionary<string, int> filesLength,
            Dictionary<string, MemoryStream>? filesContent = null, string name = "My new asset",
            string description = "My new asset description")
        {
            string assetUrl = await CreateAsset(tempFolder, catalog, filesLength, filesContent);
            AssetPost assetPost = new()
            {
                Name = name,
                Description = description,
                Url = assetUrl
            };
            var assetId = await processHelper.PostAsset(assetPost);
            Assert.That(assetId, Is.Not.EqualTo(Guid.Empty));
            return assetId;
        }

        /// <summary>
        /// Generate an array of the given length with some random content.
        /// </summary>
        /// <param name="size">Length of the array</param>
        /// <returns>The array</returns>
        public static byte[] GetRandomBytes(int size)
        {
            byte[] ret = new byte[size];
            for (int currentPos = 0; currentPos < size; )
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

        /// <summary>
        /// Compute the MD5 checksum of the given array.
        /// </summary>
        /// <param name="bytes">The array of bytes</param>
        /// <returns>The checksum (as a string)</returns>
        // ReSharper disable once MemberCanBePrivate.Global -> Kept public to be like the other helpers
        public static string ComputeMd5String(byte[] bytes)
        {
            using var md5 = MD5.Create();
            var bytesHash = md5.ComputeHash(bytes);
            return Convert.ToHexString(bytesHash);
        }

        /// <summary>
        /// Compute the MD5 checksum of the given stream.
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>The checksum (as a string)</returns>
        public static string ComputeMd5String(Stream stream)
        {
            using var md5 = MD5.Create();
            var bytesHash = md5.ComputeHash(stream);
            stream.Position = 0;
            return Convert.ToHexString(bytesHash);
        }

        /// <summary>
        /// Compute the MD5 checksum of the given array.
        /// </summary>
        /// <param name="bytes">The array of bytes</param>
        /// <returns>The checksum (as a Guid)</returns>
        public static Guid ComputeMd5Guid(byte[] bytes)
        {
            using var md5 = MD5.Create();
            var bytesHash = md5.ComputeHash(bytes);
            Assert.That(bytesHash.Length, Is.EqualTo(16));
            return new Guid(bytesHash);
        }

        /// <summary>
        /// Compute the MD5 checksum of the given stream.
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>The checksum (as a Guid)</returns>
        public static Guid ComputeMd5Guid(Stream stream)
        {
            using var md5 = MD5.Create();
            var bytesHash = md5.ComputeHash(stream);
            stream.Position = 0;
            Assert.That(bytesHash.Length, Is.EqualTo(16));
            return new Guid(bytesHash);
        }

        /// <summary>
        /// Create a memory stream from the give string.
        /// </summary>
        /// <param name="toConvert">The string</param>
        public static MemoryStream MemoryStreamFromString(string toConvert)
        {
            MemoryStream ret = new();
            StreamWriter streamWriter = new (ret);
            streamWriter.Write(toConvert);
            streamWriter.Flush();
            return ret;
        }

        /// <summary>
        /// Parse the given json string to a JsonElement.
        /// </summary>
        /// <param name="json">The json string.</param>
        public static JsonElement ParseJsonToElement(string json)
        {
            var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
