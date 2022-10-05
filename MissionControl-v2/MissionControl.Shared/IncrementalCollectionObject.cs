using System;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class IncrementalCollectionObjectExtensions
    {
        /// <summary>
        /// Small helper to create a clone of a IncrementalCollectionObject.
        /// </summary>
        /// <typeparam name="T">Type of the object to clone</typeparam>
        /// <param name="toClone">Object to clone</param>
        public static T DeepClone<T>(this T toClone) where T : IncrementalCollectionObject
        {
            T ret = (T)toClone.NewOfTypeWithId();
            ret.DeepCopy(toClone);
            return ret;
        }
    }

    /// <summary>
    /// Base class for objects of an <see cref="IncrementalCollection{T}"/>.
    /// </summary>
    public abstract class IncrementalCollectionObject: IEquatable<IncrementalCollectionObject>
    {
        /// <summary>
        /// Identifier of the object
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Method to be called by anyone modifying an <see cref="IncrementalCollectionObject"/> (after he's done with
        /// a group of modifications, no need to call it for every property) to inform that it was changed.
        /// </summary>
        public void SignalChanges()
        {
            ChangeObserver?.ObjectChanged(this);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }
            var other = (IncrementalCollectionObject)obj;
            return Id == other.Id;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object.</param>
        protected IncrementalCollectionObject(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Creates a complete independent copy of this (no data should be shared between the original and the clone).
        /// </summary>
        /// <param name="from"><see cref="IncrementalCollectionObject"/> to copy, must be same type as this.</param>
        /// <exception cref="ArgumentException">If this.GetType() != from.GetType().</exception>
        public void DeepCopy(IncrementalCollectionObject from)
        {
            if (this.GetType() != from.GetType())
            {
                throw new ArgumentException($"this.GetType() != {nameof(from)}.GetType()", nameof(from));
            }
            DeepCopyImp(from);
        }

        /// <summary>
        /// Create a new <see cref="IncrementalCollectionObject"/> of the same type as this with the same Id.
        /// </summary>
        /// <returns>The newly created <see cref="IncrementalCollectionObject"/>.</returns>
        /// <remarks>Could have been implemented through reflection, but having this virtual method results in faster
        /// code with a minimal burden on classes specializing <see cref="IncrementalCollectionObject"/>.</remarks>
        public abstract IncrementalCollectionObject NewOfTypeWithId();

        /// <summary>
        /// Method to be implemented by specializing classes to create a complete independent copy of this (no data
        /// should be shared between the original and the clone).
        /// </summary>
        /// <param name="from"><see cref="IncrementalCollectionObject"/> to copy, can be assumed to be same type as
        /// this.</param>
        protected abstract void DeepCopyImp(IncrementalCollectionObject from);

        /// <summary>
        /// Version number indicating when was this object modified with relation to the other ones in the owning
        /// collection.
        /// </summary>
        [JsonIgnore]
        internal ulong VersionNumber { get; set; }

        /// <summary>
        /// First VersionNumber of the object when it was added to the owning collection.
        /// </summary>
        [JsonIgnore]
        internal ulong FirstVersionNumber { get; set; }

        /// <summary>
        /// Object interested in changes of the <see cref="IncrementalCollectionObject"/>.
        /// </summary>
        internal IIncrementalCollectionObjectChangeObserver? ChangeObserver { get; set; }

        bool IEquatable<IncrementalCollectionObject>.Equals(IncrementalCollectionObject? other)
        {
            if (other == null)
            {
                return false;
            }

            // Remark: Omission of VersionNumber is voluntary (VersionNumber does not really define the current value
            // of the object, it has more to do with its "history").
            return Id == other.Id;
        }
    }
}
