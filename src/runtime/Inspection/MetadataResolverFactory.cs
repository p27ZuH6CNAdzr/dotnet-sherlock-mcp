using System.Reflection;
using System.Runtime.InteropServices;

namespace Sherlock.MCP.Runtime.Inspection;

internal static class MetadataResolverFactory
{
    public static PathAssemblyResolver Create(string assemblyPath, IReadOnlyList<string>? additionalSearchDirectories = null)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var paths = new HashSet<string>(comparer);
        var simpleNames = new HashSet<string>(comparer);

        var fullPath = Path.GetFullPath(assemblyPath);
        if (File.Exists(fullPath)) AddPath(paths, simpleNames, fullPath);

        AddDllsFromDirectory(paths, simpleNames, Path.GetDirectoryName(fullPath));
        AddDllsFromDirectory(paths, simpleNames, RuntimeEnvironment.GetRuntimeDirectory());

        if (additionalSearchDirectories != null)
            foreach (var directory in additionalSearchDirectories)
                AddDllsFromDirectory(paths, simpleNames, directory);

        if (NuGetCacheProbe.TryParseCacheLayout(fullPath, out var consumingTfm, out var packageId))
            foreach (var dll in NuGetCacheProbe.EnumerateCandidateDependencyDlls(consumingTfm, packageId))
                AddPath(paths, simpleNames, dll);

        return new PathAssemblyResolver(paths);
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, HashSet<string> simpleNames, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;

        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
                AddPath(paths, simpleNames, dll);
        }
        catch { }
    }

    private static void AddPath(HashSet<string> paths, HashSet<string> simpleNames, string dll)
    {
        if (!simpleNames.Add(Path.GetFileNameWithoutExtension(dll))) return;
        try { paths.Add(Path.GetFullPath(dll)); }
        catch { simpleNames.Remove(Path.GetFileNameWithoutExtension(dll)); }
    }
}
