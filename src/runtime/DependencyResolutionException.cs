namespace Sherlock.MCP.Runtime;

public sealed class DependencyResolutionException : Exception
{
    public DependencyResolutionException(string assemblyPath, IReadOnlyList<string> unresolvedDependencies)
        : base(BuildMessage(assemblyPath, unresolvedDependencies))
    {
        AssemblyPath = assemblyPath;
        UnresolvedDependencies = unresolvedDependencies;
    }

    public string AssemblyPath { get; }

    public IReadOnlyList<string> UnresolvedDependencies { get; }

    private static string BuildMessage(string assemblyPath, IReadOnlyList<string> unresolved) =>
        $"Could not resolve dependencies for '{Path.GetFileName(assemblyPath)}': {string.Join(", ", unresolved)}.";
}
