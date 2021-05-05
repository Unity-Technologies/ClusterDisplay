using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    public struct RPCMethodInfo
    {
        public readonly ushort id;
        public readonly MethodInfo methodInfo;
        public bool IsValid => methodInfo != null;

        public RPCMethodInfo (ushort _id, MethodInfo _methodInfo)
        {
            id = _id;
            methodInfo = _methodInfo;
        }
    }

    [CreateAssetMenu(fileName = "RPCRegistry", menuName = "Cluster Display/RPC Registry")]
    public partial class RPCRegistry : ScriptableObject, ISerializationCallbackReceiver
    {
        private readonly RPCMethodInfo[] rpcs = new RPCMethodInfo[ushort.MaxValue];
        public RPCMethodInfo this[ushort id] => rpcs[id];

        [SerializeField][HideInInspector] private ushort rpcCount = 0;
        public ushort RPCCount => rpcCount;

        [SerializeField][HideInInspector] private ushort[] returnedIds = new ushort[ushort.MaxValue];
        [SerializeField][HideInInspector] private ushort returnedIdsIndex = 0;

        [SerializeField][HideInInspector] private string[] serializedMethods;
        [SerializeField][HideInInspector] private ushort[] serializedIds;
        [SerializeField][HideInInspector] private ushort currentId = 0;

        [SerializeField] private IDManager<string> idManager = new IDManager<string>();

        private bool isDirty = false;

        private static string cachedRPCStubsPath = null;
        public static string RPCStubsPath
        {
            get
            {
                if (string.IsNullOrEmpty(cachedRPCStubsPath))
                    cachedRPCStubsPath = $"./RPCStubs.txt";
                return cachedRPCStubsPath;
            }
        }

        public bool TryRegisterMethod (System.Type type, MethodInfo methodInfo, out RPCMethodInfo rpcMethodInfo)
        {
            ushort id;
            if (returnedIdsIndex > 0)
                id = (ushort)(returnedIds[--returnedIdsIndex]);
            else id = currentId++;

            rpcs[id] = rpcMethodInfo = new RPCMethodInfo(id, methodInfo);
            rpcCount++;

            isDirty = true;

            Debug.Log($"Registered method with UUID: \"{id}\" for method: \"{ReflectionUtils.GetMethodSignature(methodInfo)}\" from type: \"{type.FullName}\" from assembly: \"{type.Assembly}\".");

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif

            return true;
        }

        public void UnregisterMethod (RPCMethodInfo rpcMethodInfo)
        {
            if (!rpcMethodInfo.IsValid)
                return;

            var id = rpcMethodInfo.id;
            rpcs[id] = default(RPCMethodInfo);

            returnedIds[returnedIdsIndex++] = id;
            rpcCount--;

            isDirty = true;

            Debug.Log($"Unregistered method: \"{rpcMethodInfo.methodInfo.Name}\" with UUID: \"{rpcMethodInfo.id}\".");

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        public void Clear ()
        {
            serializedMethods = null;
            serializedIds = null;

            returnedIdsIndex = 0;
            currentId = 0;
            rpcCount = 0;

            isDirty = false;

            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        public void OnAfterDeserialize()
        {
            if (serializedMethods == null || serializedIds == null)
                return;

            List<string> successfullyDeserializedMethodStrings = new List<string>(10);
            List<ushort> successfullyDeserializedIds = new List<ushort>(10);

            ushort newRpcCount = 0;
            for (ushort i = 0; i < serializedMethods.Length; i++)
            {
                if (!RPCSerializer.TryDeserializeMethodInfo(serializedMethods[i], out var methodInfo))
                {
                    Debug.LogError($"Unable to deserialize MethodInfo: \"{serializedMethods[i]}\".");
                    returnedIds[returnedIdsIndex++] = serializedIds[i];
                    continue;
                }

                Debug.Log($"Successfully deserialize MethodInfo: \"{serializedMethods[i]}\".");

                successfullyDeserializedMethodStrings.Add(serializedMethods[i]);
                successfullyDeserializedIds.Add(serializedIds[i]);

                rpcs[serializedIds[i]] = new RPCMethodInfo(serializedIds[i], methodInfo);
                newRpcCount++;
            }

            serializedMethods = successfullyDeserializedMethodStrings.ToArray();
            serializedIds = successfullyDeserializedIds.ToArray();

            rpcCount = newRpcCount;
        }

        public void OnBeforeSerialize()
        {
            if (!isDirty)
                return;

            List<string> methodList = new List<string>(10);
            List<ushort> idList = new List<ushort>(10);

            var fileStream = System.IO.File.Create(RPCStubsPath);

            for (ushort i = 0; i < currentId; i++)
            {
                if (!RPCSerializer.TrySerializeMethodInfo(ref rpcs[i], out var methodString))
                    continue;

                Debug.Log($"Serialized method: \"{methodString}\", with ID: \"{i}\".");

                methodList.Add(methodString);
                idList.Add(rpcs[i].id);

                var methodStringBytes = System.Text.Encoding.ASCII.GetBytes(methodString + "\r");
                fileStream.Write(methodStringBytes, 0, methodStringBytes.Length);
            }

            fileStream.Close();

            serializedMethods = methodList.ToArray();
            serializedIds = idList.ToArray();

            isDirty = false;
        }
    }
}
