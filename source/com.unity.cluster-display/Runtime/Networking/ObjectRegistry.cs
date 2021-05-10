using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObjectRegistry", menuName = "Cluster Display/Object Registry")]
public partial class ObjectRegistry : SingletonScriptableObject<ObjectRegistry>
{
    private readonly Object[] registeredObjects = new Object[ushort.MaxValue];
    private readonly Dictionary<Object, ushort> idLut = new Dictionary<Object, ushort>();

    private readonly IDManager idManager = new IDManager();

    public bool TryGetPipeId(Object obj, out ushort pipeId) => idLut.TryGetValue(obj, out pipeId);

    public void Register<T> (T obj) where T : Object
    {
        if (obj == null)
            throw new System.Exception($"Received NULL object to register.");

        if (!idManager.TryPopId(out var pipeId))
            throw new System.Exception("Cannot register any more objects, no more ids available.");

        registeredObjects[pipeId] = obj;
        idLut.Add(obj, pipeId);
    }

    public void Register (Object[] objects)
    {
        if (objects == null || objects.Length == 0)
            throw new System.Exception($"Received empty set of objects to register.");

        for (int i = 0; i < objects.Length; i++)
        {
            if (!idManager.TryPopId(out var pipeId))
                throw new System.Exception("Cannot register any more objects, no more ids available.");

            registeredObjects[pipeId] = objects[i];
            idLut.Add(objects[i], pipeId);
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

            if (!idLut.TryGetValue(objects[i], out var pipeId))
                continue;

            registeredObjects[pipeId] = null;
            idLut.Remove(objects[i]);
            idManager.PushId(pipeId);
        }
    }

    public void Unregister (Object obj)
    {
        if (obj == null)
            throw new System.Exception($"Received NULL object to un-register.");

        if (!idLut.TryGetValue(obj, out var pipeId))
            return;

        registeredObjects[pipeId] = null;
        idLut.Remove(obj);
        idManager.PushId(pipeId);
    }

    public void Reset ()
    {
        idLut.Clear();
        idManager.Reset();
    }
}
