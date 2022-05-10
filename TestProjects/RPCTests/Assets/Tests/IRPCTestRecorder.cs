using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    public interface IRPCTestRecorder
    {
        void RecordPropagation();
        void RecordExecution();
    }
}
