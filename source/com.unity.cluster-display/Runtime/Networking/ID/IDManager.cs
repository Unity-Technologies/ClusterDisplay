using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IDManager
{
    [SerializeField][HideInInspector] private ushort[] returnedIds = new ushort[ushort.MaxValue];
    [SerializeField][HideInInspector] private ushort returnedIdsIndex = 0;

    [SerializeField][HideInInspector] private ushort[] serializedIds = new ushort[ushort.MaxValue];

    [SerializeField][HideInInspector] private ushort serializedIdCount = 0;
    public ushort SerializedIdCount => serializedIdCount;

    [SerializeField][HideInInspector] private ushort newIdIndex = 0;
    public ushort UpperBoundID => newIdIndex;

    public bool HasSerializedData => serializedIdCount > 0;
    public ushort this[ushort index] => serializedIds[index];

    public bool TryPopId (out ushort id)
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

        serializedIds[id] = id;
        serializedIdCount++;
        return true;
    }

    public void PushId (ushort id)
    {
        returnedIds[returnedIdsIndex++] = id;
        serializedIdCount--;
    }

    protected virtual void OnClear () {}
    public void Clear ()
    {
        returnedIds = new ushort[ushort.MaxValue];
        serializedIds = new ushort[ushort.MaxValue];

        returnedIdsIndex = 0;
        serializedIdCount = 0;
        newIdIndex = 0;

        OnClear();
    }
}
