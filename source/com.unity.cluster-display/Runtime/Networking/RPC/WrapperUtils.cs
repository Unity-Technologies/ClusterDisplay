using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public static class WrapperUtils
    {
        public const string WrapperNamespace = "Unity.ClusterDisplay.RPC.Wrappers";
        public static string GetWrapperName(System.Type wrappedType) => $"{wrappedType.Name}Wrapper";
        public static string GetWrapperFullName(System.Type wrappedType) => $"{WrapperNamespace}.{wrappedType.Name}Wrapper";
        public static void GetCompilationUnitPath (System.Type wrappedType, out string wrapperName, out string folderPath, out string filePath)
        {
            wrapperName = GetWrapperName(wrappedType);
            folderPath = $"{Application.dataPath}/ClusterDisplay/Wrappers";
            filePath = $"{folderPath}/{wrapperName}.cs";

        }

        public static bool TryGetWrapperForType (System.Type wrappedType, out System.Type wrapperType)
        {
            string wrapperFullName = GetWrapperFullName(wrappedType);
            wrapperType = ReflectionUtils.GetTypeByFullName(wrapperFullName);
            return wrapperType != null;
        }
    }
}
