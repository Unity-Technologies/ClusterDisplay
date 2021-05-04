using System.Reflection;

namespace Unity.ClusterDisplay
{
    public enum TargetType 
    {
        Field,
        Property
    }

    public struct BoundTarget 
    {
        public TargetType targetType;
        public MemberInfo memberInfo;

        public System.Delegate getter;
        public System.Delegate setter;

        public BoundTarget (SerializedObjectBinding serializedObjectBinding)
        {
            targetType = serializedObjectBinding.serializedBoundTarget.targetType;
            memberInfo = serializedObjectBinding.serializedObj.GetType().GetMember(serializedObjectBinding.serializedBoundTarget.targetName)[0];
            getter = ExpressionTreeUtils.BuildGetterPropertyExpression(serializedObjectBinding.serializedObj, memberInfo);
            setter = ExpressionTreeUtils.BuildSetterPropertyExpression(serializedObjectBinding.serializedObj, memberInfo);
        }
    }

    [System.Serializable]
    public struct SerializedBoundTarget
    {
        public TargetType targetType;
        public string dataType;
        public string targetName;

        public SerializedBoundTarget (BoundTarget boundTarget)
        {
            targetType = boundTarget.targetType;

            dataType = (boundTarget.memberInfo is FieldInfo) ? 
                (boundTarget.memberInfo as FieldInfo).FieldType.FullName : 
                (boundTarget.memberInfo as PropertyInfo).PropertyType.FullName;

            targetName = boundTarget.memberInfo.Name;
        }
    }
}
