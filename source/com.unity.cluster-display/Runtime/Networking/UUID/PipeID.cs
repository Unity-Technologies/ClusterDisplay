using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PipeID
{
    [SerializeField] private ushort _uuid;


    [SerializeField] private PipeIDManager _manager;
    public PipeIDManager Manager => _manager;

    public bool IsValid => _manager != null;

    public PipeID (PipeIDManager manager, ushort targetUUID) 
    {
        _manager = manager;
        _uuid = targetUUID;
    }

    public override string ToString() => _uuid.ToString();
    public static implicit operator ushort(PipeID uuid) => uuid._uuid;

    public override int GetHashCode() => _uuid;
}
