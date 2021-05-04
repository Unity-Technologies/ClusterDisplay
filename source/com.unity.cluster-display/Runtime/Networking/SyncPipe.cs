using Unity.Collections;

public class SyncPipe : IWriteableSyncPipe, IReadableSyncPipe
{
    private NativeArray<byte> buffer;
    public ushort bufferIndex = 0;

    public NativeArray<byte> LatchAndRead()
    {
        bufferIndex = 0;
        return buffer;
    }
}
