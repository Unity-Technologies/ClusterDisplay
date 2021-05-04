using UnityEngine;

[CreateAssetMenu(fileName = "PipUUIDManager", menuName = "Cluster Display/PipelineUUIDManager")]
public class PipeIDManager : ScriptableObject
{
    [SerializeField] private ushort[] previouslyUsedIds = new ushort[ushort.MaxValue];
    [SerializeField] private ushort previouslyUsedCount = 0;
    [SerializeField] private ushort activeIdCount = 0;

    public PipeID GenerateID ()
    {
        if (previouslyUsedCount > 0)
            return new PipeID(this, previouslyUsedIds[previouslyUsedCount--]);

        var uuid = activeIdCount;
        if (activeIdCount == ushort.MaxValue)
            throw new System.Exception("No more unique 16-bit pip UUIDs available!");
        activeIdCount++;

        return new PipeID(this, uuid);
    }

    public void ReturnID (ushort uuid)
    {
        if (previouslyUsedCount + 1 > ushort.MaxValue)
            throw new System.Exception($"Cannot return UUID: {uuid}, uuid store is full.");
        previouslyUsedIds[previouslyUsedCount++] = uuid;
    }
}
