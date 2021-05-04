using UnityEngine;

[CreateAssetMenu(fileName = "PipUUIDManager", menuName = "Cluster Display/PipelineUUIDManager")]
public class PipeIDManager : ScriptableObject
{
    [SerializeField] private ushort[] previouslyUsedUUIDs = new ushort[ushort.MaxValue];
    [SerializeField] private ushort previouslyUsedCount = 0;
    [SerializeField] private ushort activeUUIDCount = 0;

    public PipeID GenerateUUID ()
    {
        if (previouslyUsedCount > 0)
            return new PipeID(this, previouslyUsedUUIDs[previouslyUsedCount--]);

        var uuid = activeUUIDCount;
        if (activeUUIDCount == ushort.MaxValue)
            throw new System.Exception("No more unique 16-bit pip UUIDs available!");
        activeUUIDCount++;

        return new PipeID(this, uuid);
    }

    public void ReturnUUID (ushort uuid)
    {
        if (previouslyUsedCount + 1 > ushort.MaxValue)
            throw new System.Exception($"Cannot return UUID: {uuid}, uuid store is full.");
        previouslyUsedUUIDs[previouslyUsedCount++] = uuid;
    }
}
