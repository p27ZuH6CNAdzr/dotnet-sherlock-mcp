namespace Sherlock.MCP.Runtime.Resources.Handlers;

using System.Globalization;
using System.Reflection;
using System.Text;
using Sherlock.MCP.Runtime.Inspection;

/// <summary>
/// Handles assembly://path/to/Lib.dll/references URIs.
/// </summary>
public class ReferencesResourceHandler : ResourceHandlerBase
{
    public ReferencesResourceHandler(IInspectionContextProvider contextProvider) : base(contextProvider)
    {
    }

    /// <summary>
    /// Match URIs like: assembly:///path/to/Lib.dll/references
    /// </summary>
    public static bool CanHandle(string uri) =>
        uri.StartsWith("assembly://", StringComparison.OrdinalIgnoreCase) &&
        uri.EndsWith("/references", StringComparison.OrdinalIgnoreCase);

    public Task<ResourceContent?> HandleAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Parse: assembly:///path/to/Lib.dll/references
        var assemblyPath = uri
            .Replace("assembly://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("/references", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart('/');

        var assembly = GetAssembly(assemblyPath);
        if (assembly is null)
            return Task.FromResult<ResourceContent?>(null);

        var content = RenderReferences(assembly);

        var resource = new ResourceContent(
            Uri: uri,
            Name: $"References for {Path.GetFileName(assemblyPath)}",
            Description: "Resolved assembly dependencies and versions",
            MimeType: "text/plain",
            Content: content); // 1 hour TTL

        return Task.FromResult<ResourceContent?>(resource);
    }

    private static string RenderReferences(Assembly assembly)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Assembly References: {assembly.GetName().Name}");
        sb.AppendLine();

        var references = assembly.GetReferencedAssemblies()
            .OrderBy(a => a.Name)
            .ToList();

        if (references.Count == 0)
        {
            sb.AppendLine("(No dependencies)");
            return sb.ToString();
        }

        sb.AppendLine("| Assembly | Version | Culture | Token |");
        sb.AppendLine("|----------|---------|---------|-------|");

        foreach (var reference in references)
        {
            var name = reference.Name ?? "?";
            var version = reference.Version?.ToString() ?? "?";
            var culture = reference.CultureInfo?.Name ?? "neutral";
            var token = FormatPublicKeyToken(reference.GetPublicKeyToken());

            sb.AppendLine(CultureInfo.InvariantCulture, $"| {name} | {version} | {culture} | {token} |");
        }

        return sb.ToString();
    }
}
