using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IDManager<T> : IDManager
{
    [SerializeField][HideInInspector] private T[] serializedObject = new T[ushort.MaxValue];
    public T GetDataByIndex(ushort index) => serializedObject[index];

    public new (ushort id, T serializedData) this[ushort index] => (base[index], serializedObject[index]);

    public bool SetData (ushort id, T data)
    {
        serializedObject[id] = data;
        return true;
    }
}
