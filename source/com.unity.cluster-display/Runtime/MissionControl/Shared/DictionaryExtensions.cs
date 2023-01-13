using System.Collections.Generic;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Returns the already existing value of the dictionary for the given key (if it contains one) or add a new
        /// value from the default constructor and returns it.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key value.</param>
        /// <typeparam name="K">Key type.</typeparam>
        /// <typeparam name="V">Value type.</typeparam>
        public static V GetOrAddNew<K, V>(this Dictionary<K, V> dictionary, K key) where V: new()
        {
            if (dictionary.TryGetValue(key, out V value))
            {
                return value;
            }
            else
            {
                V ret = new();
                dictionary[key] = ret;
                return ret;
            }
        }
    }
}
