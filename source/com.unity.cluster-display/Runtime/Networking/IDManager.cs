using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IDManager<T>
{
    [SerializeField][HideInInspector] private ushort[] returnedIds = new ushort[ushort.MaxValue];
    [SerializeField][HideInInspector] private ushort returnedIdsIndex = 0;

    [SerializeField]private ushort[] activeIds = new ushort[ushort.MaxValue];
    [SerializeField]private T[] activeObjects = new T[ushort.MaxValue];

    [SerializeField][HideInInspector] private ushort activeIdCount = 0;

    [SerializeField][HideInInspector] private ushort newIdIndex = 0;

    public bool TryPop (out ushort id)
    {
        id = 0;
        if (returnedIdsIndex > 0)
            id = (ushort)(returnedIds[--returnedIdsIndex]);

        else if (newIdIndex < ushort.MaxValue)
            id = newIdIndex++;

        else
        {
            Debug.LogError($"All ids are in use.");
            return false;
        }
            
        activeIdCount++;
        return true;
    }

    public void Push (ushort id)
    {
        returnedIds[returnedIdsIndex++] = id;
        activeIdCount--;
    }

    public void Reset ()
    {
        returnedIdsIndex = 0;
        activeIdCount = 0;
        newIdIndex = 0;
    }
}
