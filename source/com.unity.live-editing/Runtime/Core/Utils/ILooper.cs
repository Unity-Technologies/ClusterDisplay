using System;

namespace Unity.LiveEditing.LowLevel
{
    public interface ILooper
    {
        public Action Update { get; set; }
    }
}
