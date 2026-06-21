using System.Reflection;

namespace Sherlock.MCP.Runtime.Inspection;

public sealed class IsolatedRuntimeInspectionContext : IAssemblyInspectionContext
{
    private readonly DependencyResolvingLoadContext _alc;
    private string[] _unresolvedDependencies = [];

    public IsolatedRuntimeInspectionContext(string assemblyPath, IReadOnlyList<string>? additionalSearchDirectories = null)
    {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? ".";
        var contextName = $"sherlock_{Path.GetFileNameWithoutExtension(assemblyPath)}_{Guid.NewGuid():N}";

        _alc = new DependencyResolvingLoadContext(contextName, assemblyDirectory, BuildProbeFiles(assemblyPath, additionalSearchDirectories));
        Assembly = _alc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
    }

    public Assembly Assembly { get; }

    public IReadOnlyList<string> UnresolvedDependencies => _unresolvedDependencies;

    public IEnumerable<Type> GetTypes()
    {
        try
        {
            return Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            _unresolvedDependencies = DependencyDiagnostics.ExtractUnresolved(ex);
            return ex.Types.Where(t => t != null).Cast<Type>();
        }
    }

    private static List<string> BuildProbeFiles(string assemblyPath, IReadOnlyList<string>? additionalSearchDirectories)
    {
        var files = new List<string>();
        if (additionalSearchDirectories != null)
            foreach (var directory in additionalSearchDirectories)
                files.AddRange(SafeEnumerateDlls(directory));

        var fullPath = Path.GetFullPath(assemblyPath);
        if (NuGetCacheProbe.TryParseCacheLayout(fullPath, out var consumingTfm, out var packageId))
            files.AddRange(NuGetCacheProbe.EnumerateCandidateDependencyDlls(consumingTfm, packageId));

        return files;
    }

    private static string[] SafeEnumerateDlls(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                : [];
        }
        catch
        {
            return [];
        }
    }

    public MemberInfo[] GetMembers(Type type, BindingFlags flags)
    {
        try
        {
            return type.GetMembers(flags);
        }
        catch (TypeLoadException)
        {
            return [];
        }
    }

    public void Dispose()
    {
        _alc.Unload();
    }
}
