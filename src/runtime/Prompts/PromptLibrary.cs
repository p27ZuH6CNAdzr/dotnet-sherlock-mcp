namespace Sherlock.MCP.Runtime.Prompts;

/// <summary>
/// Built-in prompts for common Sherlock analysis workflows.
/// </summary>
public class PromptLibrary : IPromptProvider
{
    private static readonly IReadOnlyList<PromptDefinition> Prompts = new[]
    {
        new PromptDefinition(
            Name: "api-surface-analysis",
            Description: "Analyze public API surface of an assembly",
            Arguments: new[]
            {
                new PromptArgument("assemblyPath", "Path to .dll to analyze", Required: true)
            }),

        new PromptDefinition(
            Name: "type-hierarchy-trace",
            Description: "Get full inheritance chain and implementations for a type",
            Arguments: new[]
            {
                new PromptArgument("assemblyPath", "Path to .dll containing the type", Required: true),
                new PromptArgument("typeName", "Full type name (e.g., Namespace.ClassName)", Required: true)
            }),

        new PromptDefinition(
            Name: "method-call-graph",
            Description: "Trace what a method calls and what calls it",
            Arguments: new[]
            {
                new PromptArgument("assemblyPath", "Path to .dll containing the method", Required: true),
                new PromptArgument("typeName", "Full type name containing the method", Required: true),
                new PromptArgument("methodName", "Method name", Required: true)
            }),

        new PromptDefinition(
            Name: "dependency-inventory",
            Description: "Get all resolved dependencies (NuGet packages, .NET assemblies)",
            Arguments: new[]
            {
                new PromptArgument("assemblyPath", "Path to .dll to analyze", Required: true)
            }),

        new PromptDefinition(
            Name: "breaking-change-detection",
            Description: "Compare signatures between two assembly versions to find breaking changes",
            Arguments: new[]
            {
                new PromptArgument("oldAssemblyPath", "Path to old version of .dll", Required: true),
                new PromptArgument("newAssemblyPath", "Path to new version of .dll", Required: true)
            })
    };

    /// <inheritdoc/>
    public Task<PromptDefinition?> GetPromptAsync(string name, CancellationToken cancellationToken = default)
    {
        var prompt = Prompts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(prompt);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<PromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<PromptDefinition>>(Prompts);
    }

    /// <inheritdoc/>
    public Task<string> RenderPromptAsync(
        string name,
        Dictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = Prompts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (prompt is null)
            throw new InvalidOperationException($"Prompt '{name}' not found.");

        // Validate required arguments
        var args = arguments ?? new Dictionary<string, string>();
        foreach (var arg in prompt.Arguments.Where(a => a.Required))
        {
            if (!args.ContainsKey(arg.Name))
                throw new InvalidOperationException($"Required argument '{arg.Name}' is missing.");
        }

        // Normalize the name to lowercase for rendering
        var message = RenderMessage(prompt.Name, args);
        return Task.FromResult(message);
    }

    private static string RenderMessage(string promptName, Dictionary<string, string> arguments)
    {
        return promptName switch
        {
            "api-surface-analysis" => RenderApiSurfaceAnalysis(arguments),
            "type-hierarchy-trace" => RenderTypeHierarchyTrace(arguments),
            "method-call-graph" => RenderMethodCallGraph(arguments),
            "dependency-inventory" => RenderDependencyInventory(arguments),
            "breaking-change-detection" => RenderBreakingChangeDetection(arguments),
            _ => throw new InvalidOperationException($"Unknown prompt: {promptName}")
        };
    }

    private static string RenderApiSurfaceAnalysis(Dictionary<string, string> arguments)
    {
        var assemblyPath = arguments["assemblyPath"];
        return $@"Analyze the public API surface of {assemblyPath}.

Use these tools in order:
1. find_assembly_by_file_name or find_assembly_by_nuget_package to locate the assembly
2. get_types_from_assembly to list all public types
3. For key types, use get_type_methods (projection='summary'), get_type_properties, and get_type_events
4. Summarize the API surface with public namespaces, key types, and their main methods

Focus on the intentional public API, not internal implementation details.";
    }

    private static string RenderTypeHierarchyTrace(Dictionary<string, string> arguments)
    {
        var assemblyPath = arguments["assemblyPath"];
        var typeName = arguments["typeName"];
        return $@"Trace the full inheritance hierarchy and implementations for {typeName} in {assemblyPath}.

Use these tools:
1. find_assembly_by_file_name to locate the assembly
2. get_type_hierarchy for {typeName} to see base types and interfaces
3. find_implementations_of to find all types implementing this interface (if applicable)
4. For each related type, use get_type_info to understand its role in the hierarchy

Document the inheritance chain, abstract members, and how this type fits into the design.";
    }

    private static string RenderMethodCallGraph(Dictionary<string, string> arguments)
    {
        var assemblyPath = arguments["assemblyPath"];
        var typeName = arguments["typeName"];
        var methodName = arguments["methodName"];
        return $@"Analyze the call graph for {methodName} on {typeName} in {assemblyPath}.

Use these tools:
1. find_assembly_by_file_name to locate the assembly
2. get_method_calls to see what {methodName} invokes
3. find_references_to (with analysisDepth='il') to see what methods call into {methodName}
4. For key dependencies, use get_type_info and get_type_methods

Trace the execution flow and identify critical dependencies.";
    }

    private static string RenderDependencyInventory(Dictionary<string, string> arguments)
    {
        var assemblyPath = arguments["assemblyPath"];
        return $@"Build a complete inventory of dependencies for {assemblyPath}.

Use these tools:
1. find_assembly_by_file_name to locate the assembly
2. get_assembly_info to read referenced assemblies
3. For each reference, note the version and whether it's:
   - A standard .NET library (System.*, Microsoft.*)
   - A NuGet package (identify by name pattern)
   - An internal assembly (your organization's code)

Group dependencies by category and flag any with outdated versions or deprecated packages.";
    }

    private static string RenderBreakingChangeDetection(Dictionary<string, string> arguments)
    {
        var oldAssemblyPath = arguments["oldAssemblyPath"];
        var newAssemblyPath = arguments["newAssemblyPath"];
        return $@"Compare {oldAssemblyPath} and {newAssemblyPath} to detect breaking changes.

Use these tools:
1. find_assembly_by_file_name for both versions
2. get_types_from_assembly (projection='full') for both versions
3. For each type in old version:
   - Check if it still exists in new version (removed = breaking)
   - Use get_type_methods/properties/events to compare members (removed = breaking)
   - Look for changed method signatures (modified parameter types = breaking)
4. search_members to find deprecated members marked [Obsolete]

Summarize breaking changes by severity (requires immediate fix vs. may work with workarounds).";
    }
}
