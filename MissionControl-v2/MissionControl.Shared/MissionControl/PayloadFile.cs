using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a <see cref="Payload"/>'s file.
    /// </summary>
    /// <remarks><see cref="Asset"/> for a family portrait of <see cref="PayloadFile"/> and its relation to an
    /// <see cref="Asset"/>.</remarks>
    public class PayloadFile : IEquatable<PayloadFile>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Path of the file relative to root of the folder in which all the files are stored in
        /// preparation for the launch.</param>
        /// <param name="fileBlob">Identifier of the FileBlob that contains the file bytes.</param>
        /// <param name="compressedSize">Number of bytes of the compressed file blob.</param>
        /// <param name="size">Number of bytes of the decompressed file blob.</param>
        [JsonConstructor]
        public PayloadFile(string path, Guid fileBlob, long compressedSize, long size)
        {
            Path = path;
            FileBlob = fileBlob;
            CompressedSize = compressedSize;
            Size = size;
        }

        /// <summary>
        /// Path of the file relative to root of the folder in which all the files are stored in preparation for the
        /// launch.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Identifier of the FileBlob that contains the file bytes.
        /// </summary>
        /// <remarks>And omitted fileBlobId or a value of <see cref="Guid.Empty"/> indicates this is an empty folder
        /// that needs to be created.</remarks>
        public Guid FileBlob { get; }

        /// <summary>
        /// Number of bytes of the compressed file blob.
        /// </summary>
        public long CompressedSize { get; }

        /// <summary>
        /// Number of bytes of the decompressed file blob.
        /// </summary>
        public long Size { get; }

        /// <summary>
        /// Returns if the content referenced by this <see cref="PayloadFile"/> is the same as the given one.
        /// </summary>
        /// <param name="other">The other <see cref="PayloadFile"/>.</param>
        public bool IsSameContent(PayloadFile other)
        {
            return FileBlob == other.FileBlob && CompressedSize == other.CompressedSize && Size == other.Size;
        }

        public bool Equals(PayloadFile? other)
        {
            if (other == null || other.GetType() != typeof(PayloadFile))
            {
                return false;
            }

            return Path == other.Path && FileBlob == other.FileBlob && CompressedSize == other.CompressedSize &&
                Size == other.Size;
        }
    }
}
