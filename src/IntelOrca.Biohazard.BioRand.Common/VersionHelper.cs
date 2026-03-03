using System;
using System.Linq;
using System.Reflection;

namespace IntelOrca.Biohazard.BioRand
{
    public static class VersionHelper
    {
        public static Version GetVersion(Assembly assembly)
        {
            var version = assembly?.GetName().Version ?? new Version();
            if (version.Revision == -1)
                return version;
            return new Version(version.Major, version.Minor, version.Build);
        }

        public static string GetGitHash(Assembly assembly)
        {
            if (assembly == null)
                return string.Empty;

            var attribute = assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();
            if (attribute == null)
                return string.Empty;

            var rev = attribute.InformationalVersion;
            var plusIndex = rev.IndexOf('+');
            if (plusIndex != -1)
            {
                return rev.Substring(plusIndex + 1);
            }
            return rev;
        }

        public static string GetGitHashShort(Assembly assembly)
        {
            var hash = GetGitHash(assembly);
            return hash.Length > 7 ? hash.Substring(0, 7) : hash;
        }
    }
}
