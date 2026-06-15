using System.Reflection;

namespace BinancePulse.Services
{
    public static class VersionService
    {
        public static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly ().GetName ().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }
}