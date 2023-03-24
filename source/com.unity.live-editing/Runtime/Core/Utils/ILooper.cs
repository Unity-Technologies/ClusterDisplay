using System;

namespace Unity.LiveEditing.LowLevel
{
    /// <summary>
    /// Provides a mechanism to perform recurring operations by subscribing to the <see cref="Update"/> event.
    /// </summary>
    interface ILooper
    {
        public Action Update { get; set; }
    }
}
