namespace Sherlock.MCP.Server.Resources;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sherlock.MCP.Runtime.Resources;

/// <summary>
/// Assembly metadata resources for MCP clients.
/// Exposes queryable assembly information (types, members, metadata, references).
/// </summary>
public class SherlockResources
{
    private readonly IResourceProvider _resourceProvider;

    public SherlockResources(IResourceProvider resourceProvider)
    {
        _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
    }

    /// <summary>
    /// List available assembly metadata resource patterns.
    /// </summary>
    /// <remarks>
    /// Advertises URI patterns that clients can use to query assembly metadata.
    /// Patterns include types lists, type details, members, metadata, and references.
    /// </remarks>
    public async Task<IEnumerable<Resource>> GetAvailableResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var descriptions = await _resourceProvider.ListResourcesAsync(cancellationToken).ConfigureAwait(false);

        return descriptions.Select(d => new Resource
        {
            Uri = d.UriPattern,
            Name = d.Name,
            Description = d.Description,
            MimeType = d.MimeType
        }).ToList();
    }

    /// <summary>
    /// Read assembly metadata resource by URI.
    /// </summary>
    /// <remarks>
    /// Supports URIs like:
    /// - assembly:///path/Lib.dll/types
    /// - assembly:///path/Lib.dll/types/Namespace.Type
    /// - assembly:///path/Lib.dll/types/Namespace.Type/members
    /// - assembly:///path/Lib.dll/metadata
    /// - assembly:///path/Lib.dll/references
    /// </remarks>
    /// <param name="uri">Resource URI to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resource content as text</returns>
    public async Task<ReadResourceResult> ReadAssemblyResourceAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        var content = await _resourceProvider.GetResourceAsync(uri, cancellationToken).ConfigureAwait(false);

        if (content is null)
            throw new InvalidOperationException($"Resource not found: {uri}");

        var resourceContent = new TextResourceContents
        {
            Uri = content.Uri,
            MimeType = content.MimeType,
            Text = content.Content
        };

        return new ReadResourceResult { Contents = [resourceContent] };
    }
}
