namespace Sherlock.MCP.Runtime.Resources.Handlers;

using System.Reflection;
using System.Text.Json;
using Sherlock.MCP.Runtime.Inspection;

/// <summary>
/// Handles assembly://path/to/Lib.dll/metadata URIs.
/// </summary>
public class MetadataResourceHandler : ResourceHandlerBase
{
    public MetadataResourceHandler(IInspectionContextProvider contextProvider) : base(contextProvider)
    {
    }

    /// <summary>
    /// Match URIs like: assembly:///path/to/Lib.dll/metadata
    /// </summary>
    public static bool CanHandle(string uri) =>
        uri.StartsWith("assembly://", StringComparison.OrdinalIgnoreCase) &&
        uri.EndsWith("/metadata", StringComparison.OrdinalIgnoreCase);

    public Task<ResourceContent?> HandleAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Parse: assembly:///path/to/Lib.dll/metadata
        var assemblyPath = uri
            .Replace("assembly://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("/metadata", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart('/');

        var assembly = GetAssembly(assemblyPath);
        if (assembly is null)
            return Task.FromResult<ResourceContent?>(null);

        var metadata = RenderMetadata(assembly);

        var resource = new ResourceContent(
            Uri: uri,
            Name: $"Metadata for {Path.GetFileName(assemblyPath)}",
            Description: "Assembly identity, version, target framework, and attributes",
            MimeType: "application/json",
            Content: metadata); // 24 hour TTL

        return Task.FromResult<ResourceContent?>(resource);
    }

    private static string RenderMetadata(Assembly assembly)
    {
        var name = assembly.GetName();
        var metadata = new
        {
            name = name.Name,
            version = name.Version?.ToString(),
            culture = name.CultureInfo?.Name ?? "neutral",
            publicKeyToken = FormatPublicKeyToken(name.GetPublicKeyToken()),
            targetFramework = GetTargetFramework(assembly),
            typeCount = assembly.GetExportedTypes().Length,
            attributes = GetAssemblyAttributes(assembly),
            referencedAssemblies = assembly.GetReferencedAssemblies()
                .OrderBy(a => a.Name)
                .Select(a => new
                {
                    name = a.Name,
                    version = a.Version?.ToString(),
                    culture = a.CultureInfo?.Name ?? "neutral"
                })
                .ToList()
        };

        return JsonSerializer.Serialize(metadata, GetJsonOptions());
    }

    private static Dictionary<string, string> GetAssemblyAttributes(Assembly assembly)
    {
        var attrs = new Dictionary<string, string>();

        var company = assembly.GetCustomAttribute<System.Reflection.AssemblyCompanyAttribute>();
        if (company?.Company is not null)
            attrs["Company"] = company.Company;

        var product = assembly.GetCustomAttribute<System.Reflection.AssemblyProductAttribute>();
        if (product?.Product is not null)
            attrs["Product"] = product.Product;

        var version = assembly.GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>();
        if (version?.Version is not null)
            attrs["FileVersion"] = version.Version;

        var info = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
        if (info?.InformationalVersion is not null)
            attrs["InformationalVersion"] = info.InformationalVersion;

        return attrs;
    }
}
