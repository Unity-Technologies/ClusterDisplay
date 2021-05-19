using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IDManager<T> : IDManager where T : struct
{
    [SerializeField][HideInInspector] private T[] serializedData = new T[ushort.MaxValue];
    public T GetDataByIndex(ushort index) => serializedData[index];

    public T[] SerializedData => serializedData;

    // public new (ushort id, T serializedData) this[ushort index] => (base[index], serializedData[index]);

    public bool SetData (ushort id, T ? data)
    {
        if (!data.HasValue)
            serializedData[id] = default(T);
        else serializedData[id] = data.Value;
        return true;
    }

    protected override void OnClear()
    {
        serializedData = new T[ushort.MaxValue];
    }
}
