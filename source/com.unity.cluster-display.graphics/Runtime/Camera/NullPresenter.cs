using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class NullPresenter : IPresenter
    {
        public void Dispose() { }

        public void Initialize(GameObject gameObject)
        {
            Debug.LogError($"Using {nameof(NullPresenter)}, the current render pipeline is not supported.");
        }

        public void SetSource(RenderTexture texture) { }
    }
}
