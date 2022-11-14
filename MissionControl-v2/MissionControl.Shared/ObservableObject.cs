using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Base class for objects that we want to be notified when they changes.
    /// </summary>
    public abstract class ObservableObject
    {
        /// <summary>
        /// Event fired when the object changes
        /// </summary>
        /// <remarks>Delegates of this event shouldn't modify the object or otherwise as it is hard to manage the order
        /// in which the delegates will be called and so it can quickly create a real mess.  Anyone that want to modify
        /// the object after a change should do it after all the delegates registered to this event have been called.
        /// </remarks>
        public event Action<ObservableObject>? ObjectChanged;

        /// <summary>
        /// Method to be called manually by the code that modify the <see cref="ObservableObject"/> to inform observers 
        /// that the <see cref="ObservableObject"/> has changed.
        /// </summary>
        /// <remarks>This should not be called auto-magically by the property setter of the specializing class as this
        /// would result in too many notifications for nothing when multiple properties are changed.  This should be
        /// called manually after setting all the properties of the object.</remarks>
        public void SignalChanges()
        {
            ObjectChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// <see cref="ObservableObject"/> updates as transmitted over REST.
    /// </summary>
    public class ObservableObjectUpdate
    {
        public JsonElement Updated { get; set; }
        public ulong NextUpdate { get; set; }
    }
}
