using System.Collections.Generic;
using System.IO;
using System.Linq;
#if !UNITY_EDITOR
using Microsoft.Win32;
#endif

namespace Unity.ClusterDisplay.MissionControl
{
    public static class FolderUtils
    {
        const string k_PlayerDllName = "UnityPlayer.dll";

        public static IEnumerable<PlayerInfo> ListPlayers(string rootPath)
        {
            var dirInfo = new DirectoryInfo(rootPath);
            foreach (var directory in dirInfo.GetDirectories())
            {
                if (TryGetPlayerInfo(directory.FullName, out var playerInfo))
                {
                    yield return playerInfo;
                }
            }
        }

        public static bool TryGetPlayerInfo(string path, out PlayerInfo info)
        {
            info = default;

            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.GetFiles(k_PlayerDllName, SearchOption.TopDirectoryOnly).Length == 0)
            {
                return false;
            }

            if (dirInfo.GetFiles("*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault() is not { } exeFileInfo)
            {
                return false;
            }

            var appFileInfo = dirInfo.GetFiles("app.info", new EnumerationOptions
            {
                RecurseSubdirectories = true
            }).FirstOrDefault();

            if (appFileInfo == null)
            {
                return false;
            }

            using var lines = File.ReadLines(appFileInfo.FullName).GetEnumerator();

            if (!lines.MoveNext())
            {
                return false;
            }

            var companyName = lines.Current;

            if (!lines.MoveNext())
            {
                return false;
            }

            info = new PlayerInfo(lines.Current, companyName, exeFileInfo.FullName);

            return true;
        }

#if !UNITY_EDITOR
        public static void ClearRegistry(string companyName, string productName)
        {
            var subKey = $"Software\\{companyName}";
#pragma warning disable CA1416
            using var key = Registry.CurrentUser.OpenSubKey(subKey, true);
            key?.DeleteSubKeyTree(productName);
#pragma warning restore CA1416
        }
#endif
    }
}
