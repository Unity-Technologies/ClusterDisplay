using System.Text;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Various helpers to convert stuff...
    /// </summary>
    public static class ConvertHelpers
    {
        /// <summary>
        /// Same as in the .Net framework but we have to do it ourselves since it is only in .Net 5.0+.
        /// </summary>
        /// <param name="inArray"></param>
        /// <returns></returns>
        public static string ToHexString(byte[] inArray)
        {
            StringBuilder hashString = new();
            foreach (var currentByte in inArray)
            {
                hashString.AppendFormat("{0:x2}", currentByte);
            }
            return hashString.ToString();
        }
    }
}
