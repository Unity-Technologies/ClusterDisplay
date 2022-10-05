using System;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Information about a FileBlob managed by <see cref="FileBlobsManager"/>.
    /// </summary>
    class FileBlobInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the file blob (how <see cref="Payload"/> identifies files).</param>
        /// <param name="md5">Md5 checksum of the file blob content (stored in a <see cref="Guid"/>).</param>
        /// <param name="compressedSize">Size of the compressed file blob.</param>
        /// <param name="size">Size of the file blob (uncompressed).</param>
        public FileBlobInfo(Guid id, Guid md5, long compressedSize, long size)
        {
            Id = id;
            Md5 = md5;
            CompressedSize = compressedSize;
            Size = size;
        }

        /// <summary>
        /// Constructor to be used by json de-serialization.
        /// </summary>
        /// <param name="id">Identifier of the file blob (how <see cref="Payload"/> identifies files).</param>
        /// <param name="md5String">Md5 checksum of the file blob content (stored an hex string).</param>
        /// <param name="compressedSize">Size of the compressed file blob.</param>
        /// <param name="size">Size of the file blob (uncompressed).</param>
        [JsonConstructor]
        public FileBlobInfo(Guid id, string md5String, long compressedSize, long size)
        {
            Id = id;
            Md5 = new Guid(Convert.FromHexString(md5String));
            CompressedSize = compressedSize;
            Size = size;
        }

        /// <summary>
        /// Identifier of the file blob (how <see cref="Payload"/> identifies files).
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Md5 checksum of the file blob content (stored in a <see cref="Guid"/>).
        /// </summary>
        [JsonIgnore]
        public Guid Md5 { get; }

        /// <summary>
        /// Property to be used when serializing to serialize the md5 checksum as an hex string (it would otherwise be
        /// serialized as a GUID which would be confusing).
        /// </summary>
        [JsonPropertyName("md5")]
        public string Md5String => Convert.ToHexString(Md5.ToByteArray());

        /// <summary>
        /// Size of the compressed file blob.
        /// </summary>
        public long CompressedSize { get; }

        /// <summary>
        /// Size of the file blob (uncompressed).
        /// </summary>
        public long Size { get; }

        /// <summary>
        /// How many <see cref="Payload"/> are referencing this <see cref="FileBlobInfo"/>.
        /// </summary>
        [JsonIgnore]
        public long ReferenceCount { get; set; }

        /// <summary>
        /// Storage folder that contains this file.
        /// </summary>
        [JsonIgnore]
        public StorageFolderInfo? StorageFolder { get; set; }

        /// <summary>
        /// Next <see cref="FileBlobInfo"/> in the chain of <see cref="FileBlobInfo"/> with the same md5 checksum.
        /// </summary>
        [JsonIgnore]
        public FileBlobInfo? NextInSameMd5Chain { get; set; }

        /// <summary>
        /// Full path of the file.
        /// </summary>
        [JsonIgnore]
        public string Path
        {
            get
            {
                var filenameAsString = Id.ToString();
                return System.IO.Path.Combine(StorageFolder?.FullPath ?? "", filenameAsString.Substring(0, 2),
                    filenameAsString.Substring(2, 2), filenameAsString);
            }
        }

        /// <summary>
        /// Is the <see cref="FileBlobInfo"/> ready to be used?  Will not be ready while downloading / compressing /
        /// comparing.
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Task using this <see cref="FileBlobInfo"/>.
        /// </summary>
        [JsonIgnore]
        public List<Task> Using { get; set; } = new();
    }
}
