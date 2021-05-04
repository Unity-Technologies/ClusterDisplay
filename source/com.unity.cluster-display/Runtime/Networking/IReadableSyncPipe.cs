using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public interface IReadableSyncPipe
{
    NativeArray<byte> LatchAndRead ();
}
