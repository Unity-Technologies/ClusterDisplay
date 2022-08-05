using System;

namespace Unity.ClusterDisplay.Graphics
{
    public class PopupItemAttribute : Attribute
    {
        public PopupItemAttribute(string itemName)
        {
            ItemName = itemName;
        }

        public string ItemName { get; }
    }
}
