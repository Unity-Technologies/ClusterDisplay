﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IDManager
{
    private ushort[] returnedIds = new ushort[ushort.MaxValue];
    private ushort returnedIdsIndex = 0;

    private ushort[] serializedIds = new ushort[ushort.MaxValue];

    private ushort serializedIdCount = 0;
    public ushort SerializedIdCount => serializedIdCount;

    private ushort newIdIndex = 0;
    public ushort UpperBoundID => newIdIndex;
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

    public void PushSetOfIds (ushort[] ids, ushort largestId)
    {
        for (int i = 0; i < ids.Length; i++)
            serializedIds[ids[i]] = ids[i];
        serializedIdCount += (ushort)ids.Length;
        newIdIndex = (ushort)(largestId + 1);
    }

    public void PushUnutilizedId (ushort id)
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