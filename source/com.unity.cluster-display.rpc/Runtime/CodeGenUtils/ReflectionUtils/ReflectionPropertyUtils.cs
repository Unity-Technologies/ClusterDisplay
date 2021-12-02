using System.Linq;
using System.Reflection;

namespace Unity.ClusterDisplay
{
    public static partial class ReflectionUtils
    {
        public static bool TryGetPropertyViaAccessor (MethodInfo methodInfo, out PropertyInfo outPropertyInfo) => 
            (outPropertyInfo = methodInfo.DeclaringType.GetProperties().FirstOrDefault(propertyInfo => 
                propertyInfo.GetMethod == methodInfo || propertyInfo.SetMethod == methodInfo)) != null;

        public static bool PropertySignaturesAreEqual (PropertyInfo a, PropertyInfo b, bool matchGetSetters)
        {
            if (a.PropertyType.Assembly != b.PropertyType.Assembly ||
                a.PropertyType.FullName != b.PropertyType.FullName ||
                a.Name != b.Name)
                return false;

            return
                a.SetMethod == null && b.SetMethod == null ||
                a.SetMethod != null && b.SetMethod != null &&
                a.GetMethod == null && b.GetMethod == null ||
                a.GetMethod != null && b.GetMethod != null;
        }
    }
}
