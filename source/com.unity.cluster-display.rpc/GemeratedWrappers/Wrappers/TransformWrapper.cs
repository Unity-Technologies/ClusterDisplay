using UnityEngine;

namespace Unity.ClusterDisplay.RPC.Wrappers
{
    [RequireComponent(typeof(Transform))]
    public class TransformWrapper : ComponentWrapper<Transform>
    {
        public UnityEngine.Vector3 position
        {
            get
            {
                if (!TryGetInstance(out UnityEngine.Transform instance))
                    return default(UnityEngine.Vector3);
                return instance.position;
            }

            [ClusterRPC]
            set
            {
                if (!TryGetInstance(out UnityEngine.Transform instance))
                    return;
                instance.position = value;
            }
        }
    }
}