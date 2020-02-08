using System;
using System.Reflection;

namespace SuppressionCleanupTool
{
    internal static class Utils
    {
        public static void ResolveAssembliesWithVersionRollforward(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs e)
        {
            var requestedName = new AssemblyName(e.Name);
            if (requestedName.Version is { })
            {
                var anyVersion = (AssemblyName)requestedName.Clone();
                anyVersion.Version = null;

                var loaded = Assembly.Load(anyVersion);

                if (loaded.GetName().Version >= requestedName.Version)
                    return loaded;
            }

            return null;
        }
    }
}
