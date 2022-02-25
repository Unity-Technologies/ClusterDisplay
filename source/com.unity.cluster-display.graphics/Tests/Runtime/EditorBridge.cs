using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics.Tests
{
    public static class EditorBridge
    {
        public interface IEditorBridgeImpl
        {
            void SetGameViewSize(int width, int height);
            VolumeProfile LoadVolumeProfile(string profileName);
            void CaptureRenderDoc();
        }

        static IEditorBridgeImpl s_Impl;

        public static void SetImpl(IEditorBridgeImpl impl) => s_Impl = impl;

        public static bool SetGameViewSize(int width, int height)
        {
            if (s_Impl != null)
            {
                s_Impl.SetGameViewSize(width, height);
                return true;
            }

            return false;
        }

        public static VolumeProfile LoadVolumeProfile(string profileName)
        {
            if (s_Impl != null)
            {
                return s_Impl.LoadVolumeProfile(profileName);
            }

            return null;
        }
        
        public static bool CaptureRenderDoc()
        {
            if (s_Impl != null)
            {
                s_Impl.CaptureRenderDoc();
                return true;
            }

            return false;
        }
    }
}
