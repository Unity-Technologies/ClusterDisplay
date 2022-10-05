using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Objects that gives access to the information about a file blob in a <see cref="FileBlobsManager"/>.
    /// </summary>
    /// <remarks>The file blob in the <see cref="FileBlobsManager"/> will be locked in its current folder (cannot be
    /// deleted, moved, ...) for as long as this object hasn't been disposed.</remarks>
    public class FileBlobLock: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fileBlobInfo">Information about the file blob.</param>
        /// <param name="disposingCallback">Callback to call when disposing of <c>this</c>.</param>
        internal FileBlobLock(FileBlobInfo fileBlobInfo, Action disposingCallback)
        {
            m_FileBlobInfo = fileBlobInfo;
            m_DisposingCallback = disposingCallback;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the file blob (how <see cref="Payload"/> identifies files).</param>
        /// <param name="md5">Md5 checksum of the file blob content (stored in a <see cref="Guid"/>).</param>
        /// <param name="compressedSize">Size of the compressed file blob.</param>
        /// <param name="size">Size of the file blob (uncompressed).</param>
        /// <param name="disposingCallback">Callback to call when disposing of <c>this</c>.</param>
        internal FileBlobLock(Guid id, Guid md5, long compressedSize, long size, Action disposingCallback)
        {
            m_FileBlobInfo = new(id, md5, compressedSize, size);
            m_DisposingCallback = disposingCallback;
        }

        /// <summary>
        /// Identifier of the file blob (how <see cref="Payload"/> identifies files).
        /// </summary>
        public Guid Id => m_FileBlobInfo.Id;

        /// <summary>
        /// Md5 checksum of the file blob content (stored in a <see cref="Guid"/>).
        /// </summary>
        public Guid Md5 => m_FileBlobInfo.Md5;

        /// <summary>
        /// Md5 checksum of the file blob content.
        /// </summary>
        public string Md5String => m_FileBlobInfo.Md5String;

        /// <summary>
        /// Size of the compressed file blob.
        /// </summary>
        public long CompressedSize => m_FileBlobInfo.CompressedSize;

        /// <summary>
        /// Size of the file blob (uncompressed).
        /// </summary>
        public long Size => m_FileBlobInfo.Size;

        /// <summary>
        /// Full path of the file.
        /// </summary>
        public string Path => m_FileBlobInfo.Path;

        public void Dispose()
        {
            m_DisposingCallback();
        }

        FileBlobInfo m_FileBlobInfo;
        Action m_DisposingCallback;
    }
}
