using System;
using System.Runtime.CompilerServices;

namespace Unity.ClusterDisplay.MissionControl.Tests
{
        /// <summary>
        /// A generic object comparer that would only use object's reference,
        /// ignoring any <see cref="IEquatable{T}"/> or <see cref="object.Equals(object)"/>  overrides.
        /// </summary>
        class ObjectReferenceEqualityComparer<T> : EqualityComparer<T>
            where T : class
        {
            public new static IEqualityComparer<T> Default { get; } = new ObjectReferenceEqualityComparer<T>();

            public override bool Equals(T? x, T? y)
            {
                return ReferenceEquals(x, y);
            }

            public override int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
}
