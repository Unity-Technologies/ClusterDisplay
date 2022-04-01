using System.IO;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    internal static class WrapperUtils
    {
        /// <summary>
        /// All wrappers are placed inside this namespace.
        /// </summary>
        public const string WrapperNamespace = "Unity.ClusterDisplay.RPC.Wrappers";
        public const string WrapperPostfix = "Wrapper";

        /// <summary>
        /// This method uses the type's namespace to determine whether the type is a wrapper.
        /// </summary>
        /// <param name="wrapperType">The type we are determining.</param>
        /// <returns></returns>
        public static bool IsWrapper (System.Type wrapperType) => wrapperType.Namespace == WrapperNamespace;

        /// <summary>
        /// Simply returns "{Wrapped Type Name}Wrapper"
        /// </summary>
        /// <param name="wrappedType"></param>
        /// <returns></returns>
        public static string GetWrapperName(System.Type wrappedType) => $"{wrappedType.Name}{WrapperPostfix}";

        /// <summary>
        /// This returns the wrapper's full name which is: "{Namespace}.{Wrapped Type Name}Wrapper"
        /// </summary>
        /// <param name="wrappedType"></param>
        /// <returns></returns>
        public static string GetWrapperFullName(System.Type wrappedType) => $"{WrapperNamespace}.{wrappedType.Name}{WrapperPostfix}";

        public static bool TryGetCompilationUnitPath (System.Type type, bool typeIsWrapper, out string wrapperName, out string folderPath, out string filePath)
        {
            wrapperName = null;
            folderPath = null;
            filePath = null;

            #if UNITY_EDITOR
            if (!GeneratedWrappers.TryGetInstance(out var generatedWrappers))
                return false;

            if (!typeIsWrapper)
                wrapperName = GetWrapperName(type);
            else wrapperName = type.Name;
            var registryPath = UnityEditor.AssetDatabase.GetAssetPath(generatedWrappers);

            folderPath = $"{Path.GetDirectoryName(registryPath)}/../Wrappers";
            filePath = $"{folderPath}/{wrapperName}.cs";
            return true;
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Attempts to retrieve a wrapper for a type, and if it does not exist, this method will return false.
        /// </summary>
        /// <param name="wrappedType">The type we are wrapping.</param>
        /// <param name="wrapperType">The wrapper type that this method outputs if we find a valid type.</param>
        /// <returns></returns>
        public static bool TryGetWrapperForType (System.Type wrappedType, out System.Type wrapperType) =>
            ReflectionUtils.TryFindTypeByNamespaceAndName(WrapperNamespace, GetWrapperName(wrappedType), out wrapperType);

        public static bool TryFindWrapperImplementationType<WrappedType, WrapperType> (out System.Type wrapperImplementation)
            where WrappedType : Component
            where WrapperType : ComponentWrapper<WrappedType> =>
            ReflectionUtils.TryFindFirstDerrivedTypeFromBaseType(typeof(WrapperType), out wrapperImplementation);
    }
}
