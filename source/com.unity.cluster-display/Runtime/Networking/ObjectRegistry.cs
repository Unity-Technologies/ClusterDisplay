using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObjectRegistry", menuName = "Cluster Display/Object Registry")]
public partial class ObjectRegistry : ScriptableObject, ISerializationCallbackReceiver
{
    private readonly Dictionary<ushort, Object> objectLut = new Dictionary<ushort, Object>();
    private readonly Dictionary<Object, ushort> idLut = new Dictionary<Object, ushort>();

    private readonly ushort[] rpcLUt = new ushort[ushort.MaxValue];

    public void Register<T> (T obj) where T : Object
    {

    }

    public void Register (Object[] objects)
    {
        if (objects == null || objects.Length == 0)
            throw new System.Exception($"Received empty set of objects to register.");

        for (int i = 0; i < objects.Length; i++)
        {
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

            if (!idLut.TryGetValue(objects[i], out var id))
                continue;

            idLut.Remove(objects[i]);
            if (!objectLut.ContainsKey(id))
                continue;

            objectLut.Remove(id);
        }
    }

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
    }
}
