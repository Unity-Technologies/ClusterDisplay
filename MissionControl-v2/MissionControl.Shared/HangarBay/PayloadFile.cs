namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Information about a <see cref="Payload"/>'s file.
    /// </summary>
    public class PayloadFile: IEquatable<PayloadFile>
    {
        /// <summary>
        /// Path of the file relative to root of the folder in which all the files are stored in preparation for the
        /// launch.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Identifier of the FileBlob that contains the file bytes.
        /// </summary>
        public Guid FileBlob { get; set; }

        /// <summary>
        /// Number of bytes of the compressed file blob.
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Number of bytes of the decompressed file blob.
        /// </summary>
        public long Size { get; set; }

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
