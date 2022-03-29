using UnityEngine;

namespace Unity.ClusterDisplay.RPC.Wrappers
{
    [RequireComponent(typeof(Camera))]
    public class CameraWrapper : ComponentWrapper<Camera>
    {
        public System.Single fieldOfView
        {
            get
            {
                if (!TryGetInstance(out UnityEngine.Camera instance))
                    return default(System.Single);
                return instance.fieldOfView;
            }

            [ClusterRPC]
            set
            {
                if (!TryGetInstance(out UnityEngine.Camera instance))
                    return;
                instance.fieldOfView = value;
            }
        }
    }
}