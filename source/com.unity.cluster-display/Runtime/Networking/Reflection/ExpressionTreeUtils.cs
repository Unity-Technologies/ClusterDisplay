using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static class ExpressionTreeUtils
    {
        public static ReturnValue ExecuteGetterExpression<Instance, ReturnValue>(Object obj, System.Delegate del) where Instance : Object => ((System.Func<Instance, ReturnValue>)del)(obj as Instance);
        public static void ExecuteSetterExpression<Instance, InputValue>(Object obj, InputValue value, System.Delegate del) where Instance : Object => ((System.Action<Instance, InputValue>)del)(obj as Instance, value);

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
            var compileMethodInfo = genericExpressionType.GetMethod("Compile", new System.Type[0]);

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

        public static System.Delegate BuildGetterPropertyExpression(Object obj, MemberInfo memberInfo)
        {
            var type = ReflectionUtils.GetMemberType(memberInfo);
            var genericMethodInfo = typeof(ExpressionTreeUtils).GetMethod("BuildGetterPropertyExpression", BindingFlags.Static | BindingFlags.NonPublic);

            var genericMethod = genericMethodInfo.MakeGenericMethod(new[] { type });

            return genericMethod.Invoke(null, new object[1] { memberInfo }) as System.Delegate;
        }

        public static System.Delegate BuildSetterPropertyExpression(Object obj, MemberInfo memberInfo)
        {
            var type = ReflectionUtils.GetMemberType(memberInfo);
            var genericMethodInfo = typeof(ExpressionTreeUtils).GetMethod("BuildSetterPropertyExpression", BindingFlags.Static | BindingFlags.NonPublic);

            var genericMethod = genericMethodInfo.MakeGenericMethod(new[] { type });

            return genericMethod.Invoke(null, new object[1] { memberInfo }) as System.Delegate;
        }

        public static void BenchmarkExpression(Object obj, MemberInfo memberInfo, ref BoundTarget boundTarget)
        {
            System.Delegate del = boundTarget.getter;
            System.Type type = obj.GetType();

            Vector3 value = Vector3.zero;
            int count = 10000000;
            string countStr = count.ToString("N0");

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
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

    }
}
