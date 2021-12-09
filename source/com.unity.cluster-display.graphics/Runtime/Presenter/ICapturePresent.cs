using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    internal interface ICapturePresent
    {
        void OnBeginCapture();
        void OnEndCapture();
        CommandBuffer GetCommandBuffer();
    }
}
