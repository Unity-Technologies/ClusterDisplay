using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPipeIDContainer
{
    ushort ID { get; }
    bool ValidID { get; }
    void ApplyID(PipeID uuid);
}
