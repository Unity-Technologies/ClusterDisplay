using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [CreateAssetMenu(fileName = "ObjectRegistry", menuName = "Cluster Display/Object Registry")]
    public partial class ObjectRegistry : SingletonScriptableObject<ObjectRegistry>
    {
        private readonly Object[] registeredObjects = new Object[ushort.MaxValue];
        private readonly Dictionary<Object, ushort> pipeIdLut = new Dictionary<Object, ushort>();

        public Object this[ushort pipeId] => registeredObjects[pipeId];

        private readonly IDManager pipeIdManager = new IDManager();

        public bool TryGetPipeId(Object obj, out ushort pipeId) => pipeIdLut.TryGetValue(obj, out pipeId);

        public bool TryPopPipeId(out ushort pipeId) => pipeIdManager.TryPopId(out pipeId);
        public void PushPipeId(ushort pipeId) => pipeIdManager.PushId(pipeId);

        public void Register<T> (T obj) where T : Object
        {
            if (obj == null)
                throw new System.Exception($"Received NULL object to register.");

            if (!pipeIdManager.TryPopId(out var pipeId))
                throw new System.Exception("Cannot register any more objects, no more ids available.");

            registeredObjects[pipeId] = obj;
            pipeIdLut.Add(obj, pipeId);
        }

        public void Register (Object[] objects)
        {
            if (objects == null || objects.Length == 0)
                throw new System.Exception($"Received empty set of objects to register.");

            for (int i = 0; i < objects.Length; i++)
            {
                if (!pipeIdManager.TryPopId(out var pipeId))
                    throw new System.Exception("Cannot register any more objects, no more ids available.");

                registeredObjects[pipeId] = objects[i];
                pipeIdLut.Add(objects[i], pipeId);
            }
        }

        public void Unregister (Object[] objects)
        {
            if (objects == null || objects.Length == 0)
                throw new System.Exception($"Received empty set of objects to un-register.");

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                    continue;

                if (!pipeIdLut.TryGetValue(objects[i], out var pipeId))
                    continue;

                registeredObjects[pipeId] = null;
                pipeIdLut.Remove(objects[i]);
                pipeIdManager.PushId(pipeId);
            }
        }

        public void Unregister (Object obj)
        {
            if (obj == null)
                throw new System.Exception($"Received NULL object to un-register.");

            if (!pipeIdLut.TryGetValue(obj, out var pipeId))
                return;

            registeredObjects[pipeId] = null;
            pipeIdLut.Remove(obj);
            pipeIdManager.PushId(pipeId);
        }

        public void Reset ()
        {
            pipeIdLut.Clear();
            pipeIdManager.Reset();
        }
    }
}
