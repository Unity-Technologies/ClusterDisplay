using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static bool DetermineIfMethodIsRPCCompatible (MethodInfo methodInfo)
        {
            ushort depth = 0;
            return methodInfo
                .GetParameters()
                .All(parameterInfo => RecursivelyDetermineIfTypeIsCompatibleRPCParameter(methodInfo, parameterInfo, parameterInfo.ParameterType, ref depth));
        }

        // From https://stackoverflow.com/a/1312321
        public static string GetMethodSignatureString(this MethodInfo mi)
        {
            String[] param = mi.GetParameters()
                               .Select(p => String.Format("{0} {1}",p.ParameterType.Name,p.Name))
                               .ToArray();

            string signature = String.Format("{0} {1}({2})", mi.ReturnType.Name, mi.Name, String.Join(",", param));
            return signature;
        }

        public static bool MethodSignaturesAreEqual (MethodInfo a, MethodInfo b)
        {
            if (a.ReturnType.Assembly != b.ReturnType.Assembly ||
                a.ReturnType.FullName != b.ReturnType.FullName ||
                a.Name != b.Name)
                return false;

            var aParam = b.GetParameters();
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
