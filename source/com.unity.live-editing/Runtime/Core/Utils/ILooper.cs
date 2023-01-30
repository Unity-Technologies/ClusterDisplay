using System;

namespace Unity.LiveEditing.LowLevel
{
    /// <summary>
    ///
    /// </summary>
    public interface ILooper
    {
        public Action Update { get; set; }
    }
}
