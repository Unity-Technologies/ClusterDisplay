using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.ClusterDisplay
{
    internal partial class ClusterSync : IClusterSyncState
    {
        readonly static Dictionary<string, ClusterSync> k_Instances = new Dictionary<string, ClusterSync>();
        const string k_DefaultName = "DefaultClusterSync";

        public string InstanceName => m_InstanceName;
        readonly string m_InstanceName;

        static string m_InstanceInContext = k_DefaultName;

        internal static void PushInstance(string instanceName)
        {
            if (m_InstanceInContext == instanceName)
                return;

            if (string.IsNullOrEmpty(instanceName))
                throw new ArgumentNullException(nameof(instanceName));

            if (!k_Instances.ContainsKey(instanceName))
                throw new Exception($"Instance: \"{instanceName}\" does not exist.");

            // ClusterDebug.Log($"Pushing {nameof(ClusterSync)} instance: \"{instanceName}\".");
            m_InstanceInContext = instanceName;
        }

        internal static bool InstanceExists (string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
                throw new ArgumentNullException(nameof(instanceName));
            return k_Instances.ContainsKey(instanceName);
        }

        internal static void PopInstance()
        {
            if (m_InstanceInContext == k_DefaultName)
                return;

            // ClusterDebug.Log($"Popping {nameof(ClusterSync)} instance from: \"{m_InstanceInContext}\" back to the default instance: \"{k_DefaultName}\".");
            m_InstanceInContext = k_DefaultName;
        }

        public static ClusterSync Instance => GetUniqueInstance(m_InstanceInContext);
        public static ClusterSync GetUniqueInstance (string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                throw new ArgumentNullException(nameof(instanceName));
            }

            if (!k_Instances.TryGetValue(instanceName, out var instance))
            {
                return CreateInstance(instanceName);
            }

            return instance;
        }

        private static ClusterSync CreateInstance (string instanceName) =>
            new ClusterSync(instanceName);

        internal static void ClearInstances ()
        {
            ClusterDebug.Log($"Flushing all instances of: {nameof(ClusterSync)}.");

            // Get copy of the list of instances, since CleanUp will have
            // the instance remove itself from k_Instances, and we don't
            // wanna access the dictionary in the loop while removing things.
            var instances = k_Instances.Values.ToArray();
            foreach (var instance in instances)
            {
                instance.CleanUp();
            }

            k_Instances.Clear();
        }
    }
}
