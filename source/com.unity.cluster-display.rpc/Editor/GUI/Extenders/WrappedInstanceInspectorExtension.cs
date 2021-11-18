using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public abstract class UserWrapperInspectorExtension<InstanceType, WrapperType> : MonoBehaviourInspectorExtension<InstanceType>
        where InstanceType : MonoBehaviour
        where WrapperType : ComponentWrapper<InstanceType>
    {
        protected WrapperType cachedWrapper;

        protected override void OnPollWrapperGUI (InstanceType instance, bool hasRegistered)
        {
            if (!ReflectionUtils.TypeIsInPostProcessableAssembly(Application.dataPath, instance.GetType()))
            {
                TryGetWrapperInstance(instance, ref cachedWrapper);
                // if (hasRegistered)
                WrapperButton(instance, ref cachedWrapper);
            }
        }
    }

    public abstract class UnityWrapperInspectorExtension<InstanceType, WrapperType> : BuiltInInstanceInspectorExtension<InstanceType>
        where InstanceType : Component
        where WrapperType : ComponentWrapper<InstanceType>
    {
        protected WrapperType cachedWrapper;
        public UnityWrapperInspectorExtension(bool useDefaultInspector = true) : base(useDefaultInspector) {}

        protected override void OnPollWrapperGUI (InstanceType instance, bool anyStreamablesRegistered)
        {
            TryGetWrapperInstance(instance, ref cachedWrapper);
            // if (anyStreamablesRegistered)
            WrapperButton(instance, ref cachedWrapper);
        }
    }

    [CustomEditor(typeof(MonoBehaviour), editorForChildClasses: true)]
    public class MonoBehaviourExtension : MonoBehaviourInspectorExtension<MonoBehaviour> {}
}
