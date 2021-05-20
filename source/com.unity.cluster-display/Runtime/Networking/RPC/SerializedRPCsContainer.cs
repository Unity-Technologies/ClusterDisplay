using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializedRPCsContainer
{
    [SerializeField][HideInInspector] private SerializedRPC[] serializedData = new SerializedRPC[ushort.MaxValue];
    public SerializedRPC[] SerializedData => serializedData;

    [SerializeField] private bool[] validData = new bool[ushort.MaxValue];

    private int m_Count = 0;
    public int Count => m_Count;

    public void Foreach (System.Action<SerializedRPC> callback)
    {
        ushort rpcIndex = 0, rpcCount = 0;
        while (rpcIndex < ushort.MaxValue)
        {
            if (!validData[rpcIndex])
            {
                rpcIndex++;
                continue;
            }

            callback(serializedData[rpcIndex++]);
            if (++rpcCount >= m_Count)
                break;
        }
    }

    public bool TryGetDataByIndex(ushort index, out SerializedRPC data)
    {
        if (validData[index])
        {
            data = serializedData[index];
            return true;
        }

        data = default(SerializedRPC);
        return false;
    }

    public void SetData (ushort id, SerializedRPC ? data)
    {
        if (!data.HasValue)
        {
            serializedData[id] = default(SerializedRPC);
            validData[id] = false;
            m_Count--;
            return;
        }

        serializedData[id] = data.Value;
        validData[id] = true;
        m_Count++;
    }

    public void OnClear()
    {
        serializedData = new SerializedRPC[ushort.MaxValue];
        validData = new bool[ushort.MaxValue];
    }
}
