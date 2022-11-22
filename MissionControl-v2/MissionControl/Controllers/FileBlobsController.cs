using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/fileBlobs")]
    public class FileBlobsController : ControllerBase
    {
        public FileBlobsController(FileBlobsService fileBlobsService)
        {
            m_FileBlobsService = fileBlobsService;
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                FileBlobLock? lockedFileBlob = null;
                try
                {
                    lockedFileBlob = await m_FileBlobsService.Manager.LockFileBlob(id);
                    return File(new LockKeepingStream(lockedFileBlob), "application/gzip", true);
                }
                catch
                {
                    lockedFileBlob?.Dispose();
                    throw;
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Cannot find a file blob with the identifier {id}");
            }
        }

        /// <summary>
        /// Small wrapper around a <see cref="FileStream"/> that will keep a <see cref="FileBlobLock"/> alive for as
        /// long as the <see cref="FileStream"/> is and dispose if it when disposed (unlocking the file blob so that
        /// others can delete it if needed).
        /// </summary>
        class LockKeepingStream: FileStream
        {
            public LockKeepingStream(FileBlobLock lockedFileBlob)
                : base(lockedFileBlob.Path, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
                m_LockedFileBlob = lockedFileBlob;
            }

            public override async ValueTask DisposeAsync()
            {
                await base.DisposeAsync();
                m_LockedFileBlob?.Dispose();
                m_LockedFileBlob = null;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                m_LockedFileBlob?.Dispose();
                m_LockedFileBlob = null;
            }

            FileBlobLock? m_LockedFileBlob;
        }

        readonly FileBlobsService m_FileBlobsService;
    }
}
