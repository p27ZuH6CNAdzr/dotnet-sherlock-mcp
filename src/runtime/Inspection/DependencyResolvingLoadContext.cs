using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace Sherlock.MCP.Runtime.Inspection;

public sealed class DependencyResolvingLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly string _baseDirectory;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _additionalFiles = new(StringComparer.OrdinalIgnoreCase);

    public DependencyResolvingLoadContext(string name, string baseDirectory, IEnumerable<string>? additionalAssemblyFiles = null)
        : base(name, isCollectible: true)
    {
        _baseDirectory = baseDirectory;
        if (additionalAssemblyFiles != null)
            foreach (var file in additionalAssemblyFiles)
            {
                var simpleName = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(simpleName) && !_additionalFiles.ContainsKey(simpleName))
                    _additionalFiles[simpleName] = file;
            }
        Resolving += OnResolving;
    }

    public void Dispose()
    {
        Resolving -= OnResolving;
        Unload();
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
            return null;

        if (_loadedAssemblies.TryGetValue(name.Name, out var cached))
            return cached;

        var assemblyPath = FindAssemblyInDirectory(name) ?? FindInAdditionalFiles(name);
        if (assemblyPath == null)
            return null;

        try
        {
            var assembly = LoadFromAssemblyPath(assemblyPath);
            _loadedAssemblies[name.Name] = assembly;
            return assembly;
        }
        catch
        {
            return null;
        }
    }

    private string? FindAssemblyInDirectory(AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
            return null;

        var dllPath = Path.Combine(_baseDirectory, $"{name.Name}.dll");
        if (File.Exists(dllPath))
            return dllPath;

        var exePath = Path.Combine(_baseDirectory, $"{name.Name}.exe");
        if (File.Exists(exePath))
            return exePath;

        return null;
    }

    private string? FindInAdditionalFiles(AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name)) return null;
        return _additionalFiles.TryGetValue(name.Name, out var path) && File.Exists(path) ? path : null;
    }
}
