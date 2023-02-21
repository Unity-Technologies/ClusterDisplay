using System;
using UnityEngine.UIElements;

namespace Unity.ClusterDisplay.Editor
{
    static class UIHelpers
    {
        public static void SetHidden(this VisualElement element, bool hidden)
        {
            if (hidden)
            {
                element.AddToClassList("hidden");
            }
            else
            {
                element.RemoveFromClassList("hidden");
            }
        }
    }
}
