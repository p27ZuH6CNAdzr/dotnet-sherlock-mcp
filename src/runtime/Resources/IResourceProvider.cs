namespace Sherlock.MCP.Runtime.Resources;

/// <summary>
/// Provides queryable assembly metadata as MCP Resources.
/// Resources enable clients to fetch static assembly context without calling tools,
/// reducing token consumption and enabling context-based analysis workflows.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Get a single resource by URI.
    /// </summary>
    /// <param name="uri">Resource URI (e.g., assembly:///path/to/Lib.dll/types)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resource content or null if URI not found</returns>
    Task<ResourceContent?> GetResourceAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available resource patterns supported by this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Descriptions of resource patterns and capabilities</returns>
    Task<IEnumerable<ResourceDescription>> ListResourcesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a queryable resource pattern.
/// </summary>
public record ResourceDescription(
    string UriPattern,
    string Name,
    string Description,
    string MimeType = "text/plain");

/// <summary>
/// Content returned from a resource query.
/// </summary>
public record ResourceContent(
    string Uri,
    string Name,
    string Description,
    string MimeType,
    string Content);
