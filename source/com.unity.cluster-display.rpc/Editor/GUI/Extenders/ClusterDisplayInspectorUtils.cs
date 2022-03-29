using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using MethodGUIFunc = System.Func<UnityEngine.Component, System.Object[], System.Object[]>;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public class ClusterDisplayInspectorUtils : MonoBehaviour
    {
        private class FloatFieldAttribute : DedicatedAttribute {}
        private class DoubleFieldAttribute : DedicatedAttribute {}
        private class IntFieldAttribute : DedicatedAttribute {}
        private class BoolFieldAttribute : DedicatedAttribute {}
        private class LongFieldAttribute : DedicatedAttribute {}
        private class EnumFieldAttribute : DedicatedAttribute {}
        private class TextFieldAttribute : DedicatedAttribute {}
        private class ColorFieldAttribute : DedicatedAttribute {}
        private class Vector2FieldAttribute : DedicatedAttribute {}
        private class Vector3FieldAttribute : DedicatedAttribute {}
        private class Vector4FieldAttribute : DedicatedAttribute {}
        private class QuaternionFieldAttribute : DedicatedAttribute {}
        private class BeginHorizontalAttrikbute : DedicatedAttribute {}
        private class EndHorizontalAttrikbute : DedicatedAttribute {}
        private class FoldoutAttribute : DedicatedAttribute {}
        private class GetIndentAttribute : DedicatedAttribute {}
        private class SetIndentAttribute : DedicatedAttribute {}
        private class InvokeButtonAttribute : DedicatedAttribute {}

        [InvokeButton]
        private static bool InvokeButton (string methodName) =>
            GUILayout.Button(new GUIContent(methodName, "Invoke the selected method")/*, GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent(methodName)).x + 10)*/);

        private class InvokeMethodButtonAttribute : DedicatedAttribute {}
        private static string PrettyFieldLabel (string label) =>
            !string.IsNullOrEmpty(label) ? char.ToUpper(label[0]) + label.Substring(1): "";

        private static void FieldLabel (string label)
        {
            label = PrettyFieldLabel(label);
            EditorGUILayout.LabelField(label, GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent(label)).x));
        }

        [BeginHorizontalAttrikbute] private static void BeginHorizontal () =>                           EditorGUILayout.BeginHorizontal();
        [EndHorizontalAttrikbute] private static void EndHorizontal () =>                               EditorGUILayout.EndHorizontal();
        [Foldout] private static void Foldout (string label) =>                                         EditorGUILayout.Foldout(true, label);
        [GetIndent] private static int GetIndent() =>                                                   EditorGUI.indentLevel;
        [SetIndent] private static void SetIndent(int indentLevel) =>                                   EditorGUI.indentLevel = indentLevel;
        [FloatField] private static float FloatField(string label, float value) =>                      EditorGUILayout.FloatField(PrettyFieldLabel(label), value);
        [DoubleField] private static double DoubleField(string label, double value) =>                  EditorGUILayout.DoubleField(PrettyFieldLabel(label), value);
        [IntField] private static int IntField(string label, int value) =>                              EditorGUILayout.IntField(PrettyFieldLabel(label), value);
        [BoolField] private static bool BoolField(string label, bool value) =>                          EditorGUILayout.Toggle(PrettyFieldLabel(label), value);
        [LongField] private static long LongField(string label, long value) =>                          EditorGUILayout.LongField(PrettyFieldLabel(label), value);
        [TextField] private static string TextField(string label, string value) =>                      EditorGUILayout.TextField(PrettyFieldLabel(label), value);
        [ColorField] private static Color ColorField(string label, Color value) =>                      EditorGUILayout.ColorField(PrettyFieldLabel(label), value);
        [Vector2Field] private static Vector2 Vector2Field(string label, Vector2 value) =>              EditorGUILayout.Vector2Field(PrettyFieldLabel(label), value);
        [Vector3Field] private static Vector3 Vector3Field(string label, Vector3 value) =>              EditorGUILayout.Vector3Field(PrettyFieldLabel(label), value);
        [Vector4Field] private static Vector4 Vector4Field(string label, Vector4 value) =>              EditorGUILayout.Vector4Field(PrettyFieldLabel(label), value);
        [QuaternionField] private static Quaternion QuaternionField(string label, Quaternion value)
        {
            var newValue = EditorGUILayout.Vector3Field(PrettyFieldLabel(label), value.eulerAngles);
            return Quaternion.Euler(newValue);
        }
        [EnumField] private static EnumType EnumField<EnumType>(string label, EnumType value)
            where EnumType : Enum =>                                                            (EnumType)EditorGUILayout.EnumPopup(PrettyFieldLabel(label), value);

        private static bool TryGetExistingEditorGUIMethodForType (System.Type type, out MethodInfo methodInfo)
        {
            Type attributeType = null;
            methodInfo = null;

            if (type == typeof(float))
                attributeType = typeof(FloatFieldAttribute);
            else if (type == typeof(double))
                attributeType = typeof(DoubleFieldAttribute);
            else if (type == typeof(bool))
                attributeType = typeof(BoolFieldAttribute);
            else if (type == typeof(int))
                attributeType = typeof(IntFieldAttribute);
            else if (type == typeof(long))
                attributeType = typeof(LongFieldAttribute);
            else if (type.IsEnum)
                attributeType = typeof(EnumFieldAttribute);
            else if (type == typeof(string))
                attributeType = typeof(TextFieldAttribute);
            else if (type == typeof(Color))
                attributeType = typeof(ColorFieldAttribute);
            else if (type == typeof(Vector2))
                attributeType = typeof(Vector2FieldAttribute);
            else if (type == typeof(Vector3))
                attributeType = typeof(Vector3FieldAttribute);
            else if (type == typeof(Vector4))
                attributeType = typeof(Vector4FieldAttribute);
            else if (type == typeof(Quaternion))
                attributeType = typeof(QuaternionFieldAttribute);
            else return false;

            return ReflectionUtils.TryGetMethodWithDedicatedAttribute(attributeType, out methodInfo);
        }

        private static bool TypeIsStruct (Type type) =>
            !(type.IsPrimitive || type.IsEnum);

        private static bool TryBuildFoldoutInstructions (
            string foldoutName,
            List<Expression> instructions)
        {
            if (!ReflectionUtils.TryGetMethodWithDedicatedAttribute<FoldoutAttribute>(out var foldoutMethod))
                return false;

            instructions.Add(Expression.Call(null, foldoutMethod, Expression.Constant(PrettyFieldLabel(foldoutName))));
            return true;
        }

        private static bool TryBuildBeginIndention (
            out ParameterExpression cachedIndentionDepthVariable,
            List<ParameterExpression> localVariables,
            List<Expression> instructions)
        {
            if (!ReflectionUtils.TryGetMethodWithDedicatedAttribute<FoldoutAttribute>(out var foldoutMethod) ||
                !ReflectionUtils.TryGetMethodWithDedicatedAttribute<GetIndentAttribute>(out var getIndentionMethod) ||
                !ReflectionUtils.TryGetMethodWithDedicatedAttribute<SetIndentAttribute>(out var setIndentionMethod))
            {
                cachedIndentionDepthVariable = null;
                return false;
            }

            cachedIndentionDepthVariable = Expression.Variable(typeof(int));
            localVariables.Add(cachedIndentionDepthVariable);

            instructions.Add(Expression.Assign(cachedIndentionDepthVariable, Expression.Call(null, getIndentionMethod)));
            instructions.Add(Expression.Call(null, setIndentionMethod, Expression.Add(cachedIndentionDepthVariable, Expression.Constant(1))));
            return true;
        }

        private static void BuildEndIndentationInstructions (
            ParameterExpression cachedIndentionDepthVariable,
            List<Expression> instructions)
        {
            if (!ReflectionUtils.TryGetMethodWithDedicatedAttribute<SetIndentAttribute>(out var setIndentionMethod))
                return;
            instructions.Add(Expression.Call(null, setIndentionMethod, cachedIndentionDepthVariable));
        }

        private static bool TryRecursivelyBuildGUIInstructionsForType (
            ParameterExpression targetPropertyField,
            System.Type type, 
            string name,
            List<ParameterExpression> localVariables,
            List<Expression> instructions)
        {
            if (TryGetExistingEditorGUIMethodForType(type, out var editorGUIMethod))
            {
                var newFieldValueVariable = Expression.Variable(type);
                MethodCallExpression callExpression = null;
                if (!editorGUIMethod.IsGenericMethod)
                    callExpression = Expression.Call(editorGUIMethod, Expression.Constant(name), targetPropertyField);
                else callExpression = Expression.Call(editorGUIMethod.MakeGenericMethod(type), Expression.Constant(name), targetPropertyField);

                var newFieldAssignemntExpression = Expression.Assign(newFieldValueVariable, callExpression);
                var setPropertyOnValueChange = Expression.IfThen(
                    Expression.NotEqual(targetPropertyField, newFieldAssignemntExpression),
                    Expression.Assign(targetPropertyField, newFieldValueVariable));

                localVariables.Add(newFieldValueVariable);
                instructions.Add(setPropertyOnValueChange);
                return true;
            }

            else if (TypeIsStruct(type))
            {
                if (!TryBuildBeginIndention(out var cachedIndentionDepthVariable, localVariables, instructions))
                    return false;

                if (!TryBuildFoldoutInstructions(name, instructions))
                    return false;

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    var fieldExpression = Expression.Field(targetPropertyField, fields[fi]);
                    var fieldVariable = Expression.Variable(fields[fi].FieldType);

                    localVariables.Add(fieldVariable);
                    instructions.Add(Expression.Assign(fieldVariable, fieldExpression));

                    if (!TryRecursivelyBuildGUIInstructionsForType(
                        fieldVariable,
                        fields[fi].FieldType,
                        fields[fi].Name,
                        localVariables,
                        instructions))
                        return false;

                    instructions.Add(Expression.Assign(fieldExpression, fieldVariable));
                }

                BuildEndIndentationInstructions(cachedIndentionDepthVariable, instructions);
                return true;
            }

            return false;
        }

        public static bool TryCreatePropertyGUI (
            PropertyInfo propertyInfo, 
            Component targetInstance, 
            out Action<Component> guiMethod)
        {
            guiMethod = null;

            if (propertyInfo == null || 
                targetInstance == null ||
                (propertyInfo.GetMethod == null || !propertyInfo.GetMethod.IsPublic) || 
                (propertyInfo.SetMethod == null || !propertyInfo.SetMethod.IsPublic))
                return false;

            var componentParameter = Expression.Parameter(typeof(Component), "component");
            var castedInstanceVariable = Expression.Variable(targetInstance.GetType(), "instance");

            var instanceAssignementExpression = Expression.Assign(castedInstanceVariable, Expression.Convert(componentParameter, targetInstance.GetType()));
            var instancePropertyExpression = Expression.Property(castedInstanceVariable, propertyInfo);

            var localPropertyValueVariable = Expression.Variable(propertyInfo.PropertyType, "propertyValue");
            var localPropertyValueVariableAssignment = Expression.Assign(localPropertyValueVariable, instancePropertyExpression);

            var localVariables = new List<ParameterExpression>() { castedInstanceVariable, localPropertyValueVariable };

            var instructions = new List<Expression>() {
                instanceAssignementExpression,
                localPropertyValueVariableAssignment,
            };

            if (!TryBuildBeginIndention(out var cachedIndentionDepthVariable, localVariables, instructions))
                return false;

            if (TryGetExistingEditorGUIMethodForType(propertyInfo.PropertyType, out var editorGUIMethod))
            {
                if (propertyInfo.PropertyType.IsEnum)
                    editorGUIMethod = editorGUIMethod.MakeGenericMethod(propertyInfo.PropertyType);
                instructions.Add(Expression.Assign(localPropertyValueVariable, Expression.Call(null, editorGUIMethod, Expression.Constant(""), localPropertyValueVariable)));
                instructions.Add(Expression.IfThen(
                    Expression.NotEqual(localPropertyValueVariable, instancePropertyExpression),
                    Expression.Assign(instancePropertyExpression, localPropertyValueVariable)));
            }

            else if (TypeIsStruct(propertyInfo.PropertyType))
            {
                var fields = propertyInfo.PropertyType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    var fieldExpression = Expression.Field(localPropertyValueVariable, fields[fi]);
                    var fieldsVariable = Expression.Variable(fields[fi].FieldType);

                    localVariables.Add(fieldsVariable);
                    instructions.Add(Expression.Assign(fieldsVariable, fieldExpression));

                    if (!TryRecursivelyBuildGUIInstructionsForType(
                        fieldsVariable,
                        fields[fi].FieldType,
                        fields[fi].Name,
                        localVariables,
                        instructions))
                        return false;

                    instructions.Add(Expression.Assign(fieldExpression, fieldsVariable));
                    instructions.Add(Expression.Assign(instancePropertyExpression, localPropertyValueVariable));
                }
            }

            BuildEndIndentationInstructions(cachedIndentionDepthVariable, instructions);

            var block = Expression.Block(
                localVariables,
                instructions);

            try
            {
                var lambda = Expression.Lambda<Action<Component>>(block, componentParameter);
                guiMethod = lambda.Compile();
                return true;
            }
            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                return false;
            }
        }

        public static bool TryCreateMethodGUI (
            MethodInfo methodInfo, 
            Component targetInstance, 
            out MethodGUIFunc guiMethod)
        {
            guiMethod = null;

            if (methodInfo == null || 
                targetInstance == null ||
                !methodInfo.IsPublic)
                return false;

            var componentParameter = Expression.Parameter(typeof(Component), "component");
            var cachedArgumentsParameter = Expression.Parameter(typeof(object[]), "cachedArguments");
            var castedInstanceVariable = Expression.Variable(targetInstance.GetType(), "instance");
            var instanceAssignementExpression = Expression.Assign(castedInstanceVariable, Expression.Convert(componentParameter, targetInstance.GetType()));

            var localVariables = new List<ParameterExpression>() { castedInstanceVariable };
            var instructions = new List<Expression>() { instanceAssignementExpression };

            if (!ReflectionUtils.TryGetMethodWithDedicatedAttribute<InvokeButtonAttribute>(out var invokeButtonMethod))
                return false;

            var parameters = methodInfo.GetParameters();
            if (parameters.Length == 0)
            {

                instructions.Add(Expression.IfThen(Expression.Call(invokeButtonMethod, Expression.Constant($"Invoke: \"{methodInfo.Name}\"")), Expression.Call(castedInstanceVariable, methodInfo)));
                instructions.Add(Expression.Convert(Expression.Constant(null), typeof(object[])));
            }

            else
            {
                var valueTypeArrayType = typeof(object[]);
                var valueTypeArrayLengthProperty = valueTypeArrayType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(property => property.Name == "Length");

                var initExpressions = parameters.Select(parameter => Expression.Convert(Expression.Default(parameter.ParameterType), typeof(object)));
                instructions.Add(
                    Expression.IfThen(
                        Expression.Equal(
                            cachedArgumentsParameter,
                            Expression.Constant(null)),
                        Expression.Assign(cachedArgumentsParameter, Expression.NewArrayInit(typeof(object), initExpressions))));

                ParameterExpression[] arguments = new ParameterExpression[parameters.Length];
                for (int pi = 0; pi < parameters.Length; pi++)
                {
                    var localParameterVariable = Expression.Variable(parameters[pi].ParameterType);
                    arguments[pi] = localParameterVariable;
                    localVariables.Add(localParameterVariable);

                    var cachedArgumentsArrayAccess = Expression.ArrayAccess(cachedArgumentsParameter, Expression.Constant(pi));
                    instructions.Add(
                        Expression.IfThen(
                            Expression.Equal(cachedArgumentsArrayAccess, Expression.Constant(null)),
                            Expression.Assign(cachedArgumentsArrayAccess, Expression.Constant(Expression.Default(parameters[pi].ParameterType), typeof(object)))));

                    instructions.Add(Expression.Assign(arguments[pi], Expression.Convert(cachedArgumentsArrayAccess, parameters[pi].ParameterType)));
                }

                instructions.Add(Expression.IfThen(Expression.Call(invokeButtonMethod, Expression.Constant($"Invoke: \"{methodInfo.Name}\"")), Expression.Call(castedInstanceVariable, methodInfo, arguments)));

                for (int pi = 0; pi < parameters.Length; pi++)
                {
                    var cachedArgumentsArrayAccess = Expression.ArrayAccess(cachedArgumentsParameter, Expression.Constant(pi));
                    if (!TryRecursivelyBuildGUIInstructionsForType(
                        arguments[pi],
                        parameters[pi].ParameterType,
                        parameters[pi].Name,
                        localVariables,
                        instructions))
                        return false;

                    instructions.Add(Expression.Assign(cachedArgumentsArrayAccess, Expression.Convert(arguments[pi], typeof(object))));
                }

                instructions.Add(cachedArgumentsParameter);
            }

            var block = Expression.Block(
                localVariables,
                instructions);

            try
            {
                var lambda = Expression.Lambda<MethodGUIFunc>(block, componentParameter, cachedArgumentsParameter);
                guiMethod = lambda.Compile();
                return true;
            }
            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                return false;
            }
        }
    }
}
