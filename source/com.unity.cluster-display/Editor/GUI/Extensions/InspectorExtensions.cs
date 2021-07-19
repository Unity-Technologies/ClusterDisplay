using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public abstract class UserWrapperInspectorExtension<InstanceType, WrapperType> : UserInspectorExtension<InstanceType>
        where InstanceType : MonoBehaviour
        where WrapperType : ComponentWrapper<InstanceType>
    {
        protected WrapperType cachedWrapper;

        protected override void OnPollWrapperGUI (InstanceType instance, bool hasRegistered)
        {
            if (!ReflectionUtils.TypeIsInPostProcessableAssembly(instance.GetType()))
            {
                TryGetWrapperInstance(instance, ref cachedWrapper);
                // if (hasRegistered)
                WrapperButton(instance, ref cachedWrapper);
            }
        }
    }

    public abstract class UnityWrapperInspectorExtension<InstanceType, WrapperType> : UnityInspectorExtension<InstanceType>
        where InstanceType : Component
        where WrapperType : ComponentWrapper<InstanceType>
    {
        protected WrapperType cachedWrapper;

        protected override void OnPollWrapperGUI (InstanceType instance, bool anyStreamablesRegistered)
        {
            TryGetWrapperInstance(instance, ref cachedWrapper);
            // if (anyStreamablesRegistered)
            WrapperButton(instance, ref cachedWrapper);
        }
    }

    [CustomEditor(typeof(MonoBehaviour), editorForChildClasses: true)]
    public class MonoBehaviourExtension : UserWrapperInspectorExtension<MonoBehaviour, ComponentWrapper<MonoBehaviour>>
    {
        protected override void OnExtendInspectorGUI(MonoBehaviour instance) {}
    }

    [CustomEditor(typeof(Transform))]
    public class TransformExtension : UnityWrapperInspectorExtension<Transform, ComponentWrapper<Transform>>
    {
        protected override void OnExtendInspectorGUI(Transform instance) {}
    }

    [CustomEditor(typeof(Light), editorForChildClasses: true)]
    public class LightExtension : UnityWrapperInspectorExtension<Light, ComponentWrapper<Light>>
    {
        protected override void OnExtendInspectorGUI(Light instance) {}
    }
}
