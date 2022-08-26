namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Information about a Payload's file.
    /// </summary>
    public class PayloadFile
    {
        /// <summary>
        /// Path of the file relative to root of the folder where the asset files will be stored.
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
        /// Number of bytes taken by the file content.
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
    }
}
