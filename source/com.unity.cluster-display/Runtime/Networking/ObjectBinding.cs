using UnityEngine;

namespace Unity.ClusterDisplay
{
    public struct ObjectBinding 
    {
        public Object obj;
        public BoundTarget boundTarget;

        public ObjectBinding (SerializedObjectBinding serializedObjectBinding)
        {
            obj = serializedObjectBinding.serializedObj;
            boundTarget = new BoundTarget(serializedObjectBinding);
        }
    }

    [System.Serializable]
    public struct SerializedObjectBinding 
    {
        public Object serializedObj;
        public SerializedBoundTarget serializedBoundTarget;

        public SerializedObjectBinding (ObjectBinding objectBinding)
        {
            serializedObj = objectBinding.obj;
            serializedBoundTarget = new SerializedBoundTarget(objectBinding.boundTarget);
        }
    }
}
