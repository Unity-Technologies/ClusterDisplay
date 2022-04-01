using System;
using System.Reflection;
using System.Security.Cryptography;

namespace Unity.ClusterDisplay
{
    internal static partial class ReflectionUtils
    {
        public static string ComputeMethodHash(MethodInfo methodInfo)
        {
            using (var sha1 = SHA1Managed.Create())
            {
                string methodSignature = GetMethodSignature(methodInfo);
                
                var methodSignatureBytes = System.Text.Encoding.ASCII.GetBytes(methodSignature);
                var hashBytes = sha1.ComputeHash(methodSignatureBytes);
                
                string hashStr = BitConverter.ToString(hashBytes);
                return hashStr.Replace("-", "");
            }
        }

        public static string GenericTypeToSignature (Type typeDef)
        {
            var genericArguments = typeDef.GetGenericArguments();
            string genericTypeSignature = TypeDefToSignature(genericArguments[0]);
            for (int i = 1; i < genericArguments.Length; i++)
                genericTypeSignature = $"{genericTypeSignature},{TypeDefToSignature(genericArguments[i])}";
            return $"{ParseGenericType(typeDef)}<{genericTypeSignature}>";
        }
        
        public static string ParseGenericType(Type typeDef) =>
            typeDef.FullName.Substring(0, typeDef.FullName.Length - 2);
        
        public static string TypeDefToSignature(Type typeDef) =>
            $"{(typeDef.GetGenericArguments().Length > 0 ? GenericTypeToSignature(typeDef) : typeDef.FullName)}";

        public static string MethodParametersToSignature(MethodInfo methodDef)
        {
            var parameters = methodDef.GetParameters();
            if (parameters.Length == 0)
                return "";
            
            string parameterSignatures = TypeDefToSignature(parameters[0].ParameterType);
            for (int i = 1; i < parameters.Length; i++)
                parameterSignatures = $",{TypeDefToSignature(parameters[i].ParameterType)}";
            
            return parameterSignatures;
        }
        
        public static string GenericMethodToSignature(MethodInfo methodRef)
        {
            var genericParameters = methodRef.GetGenericArguments();
            string genericTypeSignature = TypeDefToSignature(genericParameters[0]);
            for (int i = 1; i < genericParameters.Length; i++)
                genericTypeSignature = $"{genericTypeSignature},{TypeDefToSignature(genericParameters[i])}";
            return $"{methodRef.Name}<{genericTypeSignature}>";
        }

        public static string MethodNameSignature(MethodInfo methodRef) =>
            $"{TypeDefToSignature(methodRef.DeclaringType)}.{(methodRef.GetGenericArguments().Length > 0 ? GenericMethodToSignature(methodRef) : methodRef.Name)}";

        public static string MethodParametersSignature(MethodInfo methodRef) =>
            $"{(methodRef.GetParameters().Length > 0 ? $" {MethodParametersToSignature(methodRef)}" : "")}";

        public static string GetMethodSignature(MethodInfo methodRef) =>
            $"{TypeDefToSignature(methodRef.ReturnType)} {MethodNameSignature(methodRef)}{MethodParametersSignature(methodRef)}";

        public static bool MethodSignaturesAreEqual (MethodInfo a, MethodInfo b)
        {
            if (a.ReturnType.Assembly != b.ReturnType.Assembly ||
                a.ReturnType.FullName != b.ReturnType.FullName ||
                a.Name != b.Name)
                return false;

            var aParam = a.GetParameters();
            var bParam = b.GetParameters();

            if (aParam.Length != bParam.Length)
                return false;

            bool allMatch = true;
            for (int pi = 0; pi < aParam.Length; pi++)
            {
                allMatch &=
                    aParam[pi].ParameterType.Assembly == bParam[pi].ParameterType.Assembly &&
                    aParam[pi].ParameterType.FullName == bParam[pi].ParameterType.FullName &&
                    aParam[pi].Name == bParam[pi].Name;
            }

            return allMatch;
        }
    }
}
