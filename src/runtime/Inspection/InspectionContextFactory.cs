namespace Sherlock.MCP.Runtime.Inspection;

public static class InspectionContextFactory
{
    public static IAssemblyInspectionContext Create(string assemblyPath, bool forceRuntimeLoad = false, IReadOnlyList<string>? additionalSearchDirectories = null)
    {
        if (forceRuntimeLoad)
        {
            return new IsolatedRuntimeInspectionContext(assemblyPath, additionalSearchDirectories);
        }

        return new MetadataOnlyInspectionContext(assemblyPath, additionalSearchDirectories);
    }
}

