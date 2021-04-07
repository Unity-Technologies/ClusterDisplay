using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class XRHHDRPPresenter : HDRPPresenter
    {
        public override RTHandle TargetRT 
        { 
            get { return null; } 
            set 
            { 
                if (value == null)
                    m_Camera.targetTexture = null; 
                else m_Camera.targetTexture = value;
            }
        }

        public override RTHandle PresentRT 
        {
            get => null;
            set {}
        }

        public override void Dispose() {}
        protected override void DeinitializeCamera(Camera camera) {}
        protected override void InitializeCamera(Camera camera) {}
    }
}
