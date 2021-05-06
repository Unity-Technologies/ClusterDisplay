using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace Unity.ClusterDisplay
{
    using TargetDict =
        System.Collections.Generic.Dictionary<
            UnityEngine.Object,
            System.Collections.Generic.Dictionary<
                string,
                ObjectBinding>>;

    public partial class ComponentReflectionStream : MonoBehaviour, ISerializationCallbackReceiver, IPipeIDContainer, IDataWatcher
    {
        [SerializeField] private PipeID _id;

        public ushort ID => _id;
        public bool ValidID => _id.IsValid;
        public void ApplyID(PipeID uuid) => _id = uuid;

        private readonly TargetDict targets = new TargetDict();
        [HideInInspector] [SerializeField] private SerializedObjectBinding[] serializedTargets;

        [SerializeField] private Object[] objects = new Object[0];

        private bool HasTarget(Object obj, string memberName)
        {
            return
                !string.IsNullOrEmpty(memberName) &&
                targets.TryGetValue(obj, out var members) &&
                members.ContainsKey(memberName);
        }

        public void Add(Object obj, MemberInfo memberInfo)
        {
            if (objects == null)
                objects = new Object[1] { obj };

            else if (!objects.Contains(obj))
            {
                var newObjects = new Object[objects.Length + 1];
                System.Array.Copy(objects, 0, newObjects, 0, objects.Length);
                newObjects[newObjects.Length - 1] = obj;
                objects = newObjects;
            }

            var target = new BoundTarget
            {
                targetType = memberInfo is FieldInfo ? TargetType.Field : TargetType.Property,
                memberInfo = memberInfo,
                getter = ExpressionTreeUtils.BuildGetterPropertyExpression(obj, memberInfo),
                setter = ExpressionTreeUtils.BuildSetterPropertyExpression(obj, memberInfo)
            };

            if (targets.TryGetValue(obj, out var members))
            {
                if (members.TryGetValue(memberInfo.Name, out var objTarget))
                {
                    objTarget.boundTarget = target;
                    members[memberInfo.Name] = objTarget;
                    return;
                }

                members.Add(memberInfo.Name, new ObjectBinding
                {
                    obj = obj,
                    boundTarget = target
                });
                return;
            }

            targets.Add(obj, new Dictionary<string, ObjectBinding>
            {
                { memberInfo.Name, new ObjectBinding
                {
                    obj = obj,
                    boundTarget = target
                }}
            });
        }

        private void Remove(Object obj, string memberName)
        {
            if (targets.TryGetValue(obj, out var members))
                members.Remove(memberName);
        }

        private void Awake()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync))
                return;
            clusterSync.FrameDataManager.RegisterWatcher(this);
        }

        public void OnDestroy()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync, throwException: false))
                return;
            clusterSync.FrameDataManager.UnregisterWatcher(_id);
        }

        public void Reset()
        {
            if (!ClusterSync.TryGetInstance(out var clusterSync))
                return;

            if (_id.IsValid)
                clusterSync.FrameDataManager.UnregisterWatcher(this._id);
            clusterSync.FrameDataManager.RegisterWatcher(this);
        }

        public void Clear()
        {
            serializedTargets = null;
            targets.Clear();
            objects = new Object[0];
        }

        private void SetupTargets()
        {
            targets.Clear();

            if (serializedTargets == null)
                return;

            for (int i = 0; i < serializedTargets.Length; i++)
            {
                if (serializedTargets[i].serializedObj == null)
                    continue;

                var type = serializedTargets[i].serializedObj.GetType();
                var members = type.GetMember(serializedTargets[i].serializedBoundTarget.targetName);
                if (members == null || members.Length == 0)
                    continue;

                if (targets.TryGetValue(serializedTargets[i].serializedObj, out var memberLut))
                    memberLut.Add(serializedTargets[i].serializedBoundTarget.targetName, new ObjectBinding(serializedTargets[i]));

                else targets.Add(serializedTargets[i].serializedObj, new Dictionary<string, ObjectBinding> { { serializedTargets[i].serializedBoundTarget.targetName, new ObjectBinding(serializedTargets[i]) } });
            }
        }

        public void OnBeforeSerialize()
        {
            if (targets == null || targets.Count == 0)
                return;

            var targetList = new List<SerializedObjectBinding>();

            foreach (var targetPair in targets)
                foreach (var memberPair in targetPair.Value)
                    targetList.Add(new SerializedObjectBinding(memberPair.Value));

            serializedTargets = targetList.ToArray();

    #if UNITY_EDITOR
            var stateList = new List<ObjectUIState>();
            foreach (var statePair in states)
            {
                if (!targets.ContainsKey(statePair.Key))
                    continue;

                stateList.Add(statePair.Value);
            }
            serializedStates = stateList.ToArray();
    #endif
        }

        public void OnAfterDeserialize() => SetupTargets();
    }
}
