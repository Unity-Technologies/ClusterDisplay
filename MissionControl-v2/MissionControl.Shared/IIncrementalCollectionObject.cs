using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class IncrementalCollectionObjectExtensions
    {
        /// <summary>
        /// Small helper to create a clone of a IncrementalCollectionObject.
        /// </summary>
        /// <typeparam name="T">Type of the object to clone</typeparam>
        /// <param name="toClone">Object to clone</param>
        public static T DeepClone<T>(this T toClone) where T : IIncrementalCollectionObject
        {
            T ret = ObjectWithIdFactory<T>.ConstructNewObject(toClone.Id);
            ret.DeepCopyFrom(toClone);
            return ret;
        }

        /// <summary>
        /// Indicate that there was changes made to this object (part of a collection tracking its changes).
        /// </summary>
        /// <typeparam name="T">The type of object that changed.</typeparam>
        /// <param name="withChanges">The object that has changed.</param>
        /// <param name="owningCollection">Collection tracking changes of <see cref="withChanges"/>.</param>
        /// <remarks>To be called after the modifications have been done.
        /// <br/><br/>Many changes on a single object can be batched before calling this method to signal them all in a
        /// single call.</remarks>
        public static void SignalChanges<T>(this T withChanges, IncrementalCollection<T> owningCollection)
            where T : IIncrementalCollectionObject
        {
            owningCollection.SignalObjectChanged(withChanges);
        }
    }

    /// <summary>
    /// Helper class (and method) to allow constructing a new object that take a Guid as parameter for the constructor.
    /// </summary>
    /// <typeparam name="T">Type of object to construct</typeparam>
    /// <remarks>The goal is to avoid using reflection (barely more complex and faster).</remarks>
    public static class ObjectWithIdFactory<T>
    {
        static ObjectWithIdFactory()
        {
            ConstructorInfo? constructorInfo = typeof(T).GetConstructor(new[] { typeof(Guid) });
            Debug.Assert(constructorInfo != null);

            DynamicMethod newObjectWithIdMethod = new($"{typeof(T).FullName}.ConstructorWithId",
                typeof(T), new[] { typeof(Guid) });
            var gen = newObjectWithIdMethod.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Newobj, constructorInfo);
            gen.Emit(OpCodes.Ret);

            ConstructNewObject = newObjectWithIdMethod.CreateDelegate<Func<Guid, T>>();
        }

        public static Func<Guid, T> ConstructNewObject { get; }
    }

    /// <summary>
    /// Interface to be implemented by objects added to an <see cref="IncrementalCollection{T}"/>.
    /// </summary>
    public interface IIncrementalCollectionObject
    {
        /// <summary>
        /// Identifier of the object
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Creates a complete independent of from (no data should be shared between the original and the this).
        /// </summary>
        /// <param name="from"><see cref="IIncrementalCollectionObject"/> to copy from, must be same type as this.
        /// </param>
        public void DeepCopyFrom(IIncrementalCollectionObject from);
    }
}
