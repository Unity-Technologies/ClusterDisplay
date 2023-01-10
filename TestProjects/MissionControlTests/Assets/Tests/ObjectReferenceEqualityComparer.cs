using System;
// ReSharper disable once RedundantUsingDirective -> Need when compiling in Unity
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Unity.ClusterDisplay.MissionControl
{
        /// <summary>
        /// A generic object comparer that would only use object's reference,
        /// ignoring any <see cref="IEquatable{T}"/> or <see cref="object.Equals(object)"/>  overrides.
        /// </summary>
        class ObjectReferenceEqualityComparer<T> : EqualityComparer<T>
            where T : class
        {
            public new static IEqualityComparer<T> Default { get; } = new ObjectReferenceEqualityComparer<T>();

#if UNITY_64
            public override bool Equals(T x, T y)
#else
            public override bool Equals(T? x, T? y)
#endif
            {
                return ReferenceEquals(x, y);
            }

            public override int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
}
