using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPipeIDContainer
{
    ushort UUID { get; }
    bool ValidUUID { get; }
    void ApplyUUID(PipeID uuid);
}
