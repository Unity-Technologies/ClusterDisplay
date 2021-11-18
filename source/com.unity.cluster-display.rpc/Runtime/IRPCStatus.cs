using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRPCStatus
{
    bool GetStatus(ushort rpcId);
    void SetStatus(ushort rpcId);
}
