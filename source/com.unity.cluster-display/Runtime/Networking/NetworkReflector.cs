/*
using BinarySerialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;
using ArraySegment = System.ArraySegment<byte>;
using BitConverter = System.BitConverter;
using Stopwatch = System.Diagnostics.Stopwatch;
using TargetDict =
    System.Collections.Generic.Dictionary<
        UnityEngine.Object,
        System.Collections.Generic.Dictionary<
            string,
            ObjectBinding>>;
using Type = System.Type;

[DefaultExecutionOrder(100)]
public partial class NetworkReflector : MonoBehaviour, ISerializationCallbackReceiver, IPackData, IUnpackData {
    public enum NetworkReflectorMode {
        Send,
        Receive
    }

    public NetworkReflectorMode mode;
    [SerializeField] private string id;
    public string ID => id;
    private byte[] cachedIdBytes;

    [SerializeField] private Object[] objects;
    [HideInInspector] [SerializeField] private SerializedObjectBinding[] serializedTargets;

    private readonly TargetDict targets = new TargetDict();
    private readonly Dictionary<Type, System.Delegate> toBytesExpresssions = new Dictionary<Type, System.Delegate>();
    private readonly Dictionary<Type, System.Delegate> fromBytesExpresssions = new Dictionary<Type, System.Delegate>();

    private ReturnValue ExecuteGetterExpression<Instance, ReturnValue>(Object obj, System.Delegate del) where Instance : Object
    {
        return ((System.Func<Instance, ReturnValue>)del)(obj as Instance);
    }

    private void ExecuteSetterExpression<Instance, InputValue>(Object obj, InputValue value, System.Delegate del) where Instance : Object
    {
        ((System.Action<Instance, InputValue>)del)(obj as Instance, value);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString("N").ToUpper();
    }

    private void Start()
    {
        SetupTargets();

        if (NetworkReflectorManager.TryGetInstance(out var networkReflectorManager))
            networkReflectorManager.Register(this);

        if (cachedIdBytes == null)
        {
            cachedIdBytes = Encoding.ASCII.GetBytes(ID);
            System.Array.Copy(cachedIdBytes, buffer, cachedIdBytes.Length);
            bufferIndex = cachedIdBytes.Length;
        }
    }

    private MemoryStream memoryStream = null;
    void LateUpdate()
    {
        BinarySerializer binarySerializer = new BinarySerializer();

        if ( memoryStream == null)
            memoryStream = new MemoryStream(buffer);

        bufferIndex = cachedIdBytes.Length;
        memoryStream.Seek(bufferIndex, SeekOrigin.Begin);

        foreach (var targetPair in targets)
        {
            foreach (var memberPair in targetPair.Value)
            {
                var del = memberPair.Value.boundTarget.getter;
                var vector = ExecuteGetterExpression<Transform, Vector3>(targetPair.Key, del);

                byte[] xBytes = BitConverter.GetBytes(vector.x);
                byte[] yBytes = BitConverter.GetBytes(vector.y);
                byte[] zBytes = BitConverter.GetBytes(vector.z);

                memoryStream.Write(xBytes, 0, xBytes.Length);
                memoryStream.Write(yBytes, 0, yBytes.Length);
                memoryStream.Write(zBytes, 0, zBytes.Length);
            }
        }

        bufferIndex = (int)memoryStream.Position;
    }

    private static Type GetMemberType(MemberInfo memberInfo)
    {
        return
            memberInfo is FieldInfo ?
            (memberInfo as FieldInfo).FieldType :
            (memberInfo as PropertyInfo).PropertyType;
    }

    private bool HasTarget(Object obj, string memberName)
    {
        return
            !string.IsNullOrEmpty(memberName) &&
            targets.TryGetValue(obj, out var members) &&
            members.ContainsKey(memberName);
    }

    private static System.Delegate BuildGetterPropertyExpression<T>(MemberInfo memberInfo)
    {
        var propertyInfo = memberInfo as PropertyInfo;
        var dataType = propertyInfo.PropertyType;

        var instance = Expression.Parameter(propertyInfo.DeclaringType, "instance");
        var property = Expression.Property(instance, propertyInfo);
        var convert = Expression.Convert(property, dataType);

        var lambda = Expression.Lambda(convert, instance);
        Debug.Log($"Compiling lambda expression: \"{lambda}\" to GET value of type: \"{dataType.FullName}\" from property member: \"{propertyInfo.Name}\" in class: \"{propertyInfo.DeclaringType.FullName}\".");

        try
        {
            return lambda.Compile();
        } catch (System.Exception exception)
        {
            Debug.LogError("Unable to compile lambda expression, the following exception occurred:");
            Debug.LogException(exception);
            return null;
        }
    }

    private static MethodCallExpression BuildLambdaToConvertPrimitiveToBytes (Type primitiveType)
    {
        var getBytesMethodInfo = typeof(BitConverter).GetMethod("GetBytes", new Type[1] { primitiveType });
        var parameter = Expression.Parameter(primitiveType, "primitiveValue");
        var getBytesCallExpression = Expression.Call(getBytesMethodInfo, parameter);
        return getBytesCallExpression;
    }

    // This builds a delegate of type: Action<PropertyInfo.DeclaringType, PropertyInfo.PropertyType> allowing us
    // to modify the property on some object instance
    private static System.Delegate BuildSetterPropertyExpression<T>(MemberInfo memberInfo)
    {
        var propertyInfo = memberInfo as PropertyInfo;
        var dataType = propertyInfo.PropertyType;

        // Input instance with the property we want to modify.
        var instance = Expression.Parameter(propertyInfo.DeclaringType, "instance");

        // Input value that we want to apply to our property.
        var value = Expression.Parameter(dataType, "value");

        // The property we want to modify on our instance.
        var property = Expression.Property(instance, propertyInfo);

        // The expression that actually sets the property with our value.
        var expression = Expression.Assign(property, value); // The result here should be "instance.property = value;".
        

        // Building System.Action<PropertyInfo.DeclaryingType, PropertyInfo.PropertyType>
        var genericActionType = typeof(System.Action<,>).MakeGenericType(new[] { propertyInfo.DeclaringType, dataType });

        // Building parameter type array to find overriding Lambda method with target parameters.
        var targetLambdaParameters = new[] { typeof(Expression), typeof(ParameterExpression[]) };

        // Get the target overriding Lamba method.
        var targetLambdaMethodInfo = typeof(Expression).GetMethods().Where(methodInfo =>
        {
            if (!methodInfo.IsStatic ||
                !methodInfo.IsPublic ||
                methodInfo.Name != "Lambda" ||
                !methodInfo.IsGenericMethodDefinition)
                return false;

            var parameters = methodInfo.GetParameters();
            return parameters.All(parameter => targetLambdaParameters.Contains(parameter.ParameterType));

        }).FirstOrDefault();

        if (targetLambdaMethodInfo == null)
        {
            Debug.LogError("Unable to find target Expression.Lambda function.");
            return null;
        }

        // Lambda method takes generic parameters, so build generic method using our action type.
        var genericLambdaMethod = targetLambdaMethodInfo.MakeGenericMethod(new[] { genericActionType });

        var lambdaParameters = new ParameterExpression[2] { instance, value };

        // Invoke Expression.Lambda(Expression, params ParameterExpression[]);
        var lambdaObj = genericLambdaMethod.Invoke(null, new object[2] { expression, lambdaParameters });

        // Get MethodInfo for Lambda.Compile().
        var genericExpressionType = typeof(Expression<>).MakeGenericType(new[] { genericActionType });
        var compileMethodInfo = genericExpressionType.GetMethod("Compile", new Type[0]);

        Debug.Log($"Compiling lambda expression: \"{lambdaObj}\" to SET value of type: \"{dataType.FullName}\" on property member: \"{propertyInfo.Name}\" in class: \"{propertyInfo.DeclaringType.FullName}\".");

        try
        {
            // Invoke Lambda.Compile() and return the delegate.
            // The result here should be somethng like:
            // "(PropertyInfo.DeclaringType instance, PropertyInfo.ProeprtyType value) => instance.{property name} = value;"
            return (System.Delegate)compileMethodInfo.Invoke(lambdaObj, new object[0]);
        } catch (System.Exception exception)
        {
            Debug.LogError("Unable to compile lambda expression, the following exception occurred:");
            Debug.LogException(exception);
            return null;
        }
    }

    public static System.Delegate BuildGetterPropertyExpression(Object obj, MemberInfo memberInfo)
    {
        var type = GetMemberType(memberInfo);
        var genericMethodInfo = typeof(NetworkReflector).GetMethod("BuildGetterPropertyExpression", BindingFlags.NonPublic | BindingFlags.Static);

        var genericMethod = genericMethodInfo.MakeGenericMethod(new[] { type });

        return genericMethod.Invoke(null, new object[1] { memberInfo }) as System.Delegate;
    }

    public static System.Delegate BuildSetterPropertyExpression(Object obj, MemberInfo memberInfo)
    {
        var type = GetMemberType(memberInfo);
        var genericMethodInfo = typeof(NetworkReflector).GetMethod("BuildSetterPropertyExpression", BindingFlags.NonPublic | BindingFlags.Static);

        var genericMethod = genericMethodInfo.MakeGenericMethod(new[] { type });

        return genericMethod.Invoke(null, new object[1] { memberInfo }) as System.Delegate;
    }

    private void BenchmarkExpression(Object obj, MemberInfo memberInfo)
    {
        System.Delegate del = targets[obj][memberInfo.Name].boundTarget.getter;
        System.Type type = obj.GetType();

        Vector3 value = Vector3.zero;
        int count = 10000000;
        string countStr = count.ToString("N0");

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // Retrieve the value using reflection via GetValue.
        for (int i = 0; i < count; i++)
            value = (Vector3)((memberInfo is FieldInfo) ?
                        (memberInfo as FieldInfo).GetValue(obj) :
                        (memberInfo as PropertyInfo).GetValue(obj));

        stopwatch.Stop();
        Debug.Log($"(TIME) GetValue called {countStr} times: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");

        stopwatch.Reset();
        stopwatch.Start();

        // Execute expression tree to retrieve the value.
        for (int i = 0; i < count; i++)
            value = ExecuteGetterExpression<Transform, Vector3>(obj, del);

        stopwatch.Stop();
        Debug.Log($"(TIME) Expression called {countStr} times: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");

        stopwatch.Reset();
        stopwatch.Start();

        // Uncomment this if you want to test a specific object.
        // Retrieve the value directly from the object.
        for (int i = 0; i < count; i++)
            value = (obj as Transform).position;

        stopwatch.Stop();

        Debug.Log($"(TIME) Directly called {countStr} times: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");
    }

    public void Clear()
    {
        serializedTargets = null;
        targets.Clear();
        objects = null;
    }

    private void Remove(Object obj, string memberName)
    {
        if (targets.TryGetValue(obj, out var members))
            members.Remove(memberName);
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
            getter = BuildGetterPropertyExpression(obj, memberInfo),
            setter = BuildSetterPropertyExpression(obj, memberInfo)
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

    public static (FieldInfo[], PropertyInfo[]) GetAllValueTypeFieldsAndProperties(Type targetType)
    {
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        List<FieldInfo> fields = targetType.GetFields(bindingFlags).ToList();
        List<PropertyInfo> properties = targetType.GetProperties(bindingFlags).ToList();

        List<FieldInfo> serializedFields = new List<FieldInfo>();
        List<PropertyInfo> serializedProperties = new List<PropertyInfo>();

        System.Type baseType = targetType.BaseType;

        while (baseType != typeof(object))
        {
            fields.AddRange(baseType.GetFields(bindingFlags));
            properties.AddRange(baseType.GetProperties(bindingFlags).Where(propertyInfo => propertyInfo.GetGetMethod(true) != null));
            baseType = baseType.BaseType;
        }

        var distinctFields = fields.Distinct();
        var distinctProperties = properties.Distinct();

        foreach (var field in distinctFields)
        {
            if (!field.FieldType.IsValueType || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null))
                continue;

            serializedFields.Add(field);
        }

        foreach (var property in distinctProperties)
        {
            if (!property.PropertyType.IsValueType)
                continue;
            serializedProperties.Add(property);
        }

        return (serializedFields.ToArray(), serializedProperties.ToArray());
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

    private readonly byte[] buffer = new byte[1024];
    private int bufferIndex;

    public byte[] Buffer => buffer;
    public int BufferSize => bufferIndex;

    public void UnpackBytes(ArraySegment bytes)
    {
        int byteOffset = 0;
        foreach (var targetPair in targets)
        {
            foreach (var memberPair in targetPair.Value)
            {
                float x = BitConverter.ToSingle(bytes.Array, 32 + byteOffset);
                float y = BitConverter.ToSingle(bytes.Array, 32 + byteOffset + 4);
                float z = BitConverter.ToSingle(bytes.Array, 32 + byteOffset + 8);
                byteOffset += 12;

                var vector = new Vector3(x, y, z);

                var del = memberPair.Value.boundTarget.setter;
                ExecuteSetterExpression<Transform, Vector3>(targetPair.Key, vector, del);
            }
        }
    }
}
*/
