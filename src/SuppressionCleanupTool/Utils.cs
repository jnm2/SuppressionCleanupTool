using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

        public static ImmutableDictionary<TKey, TValue> GroupToImmutableDictionary<TSource, TKey, TValue>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<IEnumerable<TSource>, TValue> valueSelector,
            IEqualityComparer<TKey> keyComparer = null)
        {
            return ImmutableDictionary.CreateRange(
                keyComparer,
                source.GroupBy(
                    keySelector,
                    (key, values) => new KeyValuePair<TKey, TValue>(key, valueSelector.Invoke(values)),
                    keyComparer));
        }
    }
}
