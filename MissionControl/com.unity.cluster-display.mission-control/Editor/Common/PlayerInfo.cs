using System.IO;

namespace Unity.ClusterDisplay.MissionControl
{
    public readonly struct PlayerInfo
    {
        public string ProductName { get; }
        public string CompanyName { get; }
        public string ExecutablePath { get; }

        public string DirectoryName => new DirectoryInfo(ExecutablePath).Parent?.Name;

        public override string ToString()
        {
            if (string.IsNullOrEmpty(ProductName) || string.IsNullOrEmpty(CompanyName))
            {
                return $"{DirectoryName} [Non-Unity application {new FileInfo(ExecutablePath).Name}]";
            }

            return $"{DirectoryName} [{ProductName} - {CompanyName}]";
        }

        public PlayerInfo(string productName, string companyName, string executablePath)
        {
            ProductName = productName;
            CompanyName = companyName;
            ExecutablePath = executablePath;
        }
    }
}
