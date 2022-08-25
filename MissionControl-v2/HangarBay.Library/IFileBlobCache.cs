using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Interface for the main methods of <see cref="FileBlobCache"/> (to help making the code easier to test).
    /// </summary>
    /// <remarks>Since the main goal of this interface is testability of the code, see <see cref="FileBlobCache"/> for
    /// the description of methods.</remarks>
    public interface IFileBlobCache
    {
        void IncreaseUsageCount(Guid fileBlobId, long compressedSize, long size);
        void DecreaseUsageCount(Guid fileBlobId);
    }
}
