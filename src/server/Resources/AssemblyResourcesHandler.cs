namespace Sherlock.MCP.Server.Resources;

using ModelContextProtocol.Protocol;
using Sherlock.MCP.Runtime.Resources;

/// <summary>
/// MCP Resource handler that exposes assembly metadata as queryable resources.
/// Wired via request filters in Program.cs rather than attributes.
/// </summary>
public class AssemblyResourcesHandler
{
    private readonly IResourceProvider _resourceProvider;

    public AssemblyResourcesHandler(IResourceProvider resourceProvider)
    {
        _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
    }

    /// <summary>
    /// Handle ListResourcesRequest - advertise available resource patterns.
    /// </summary>
    public async Task<ListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var descriptions = await _resourceProvider.ListResourcesAsync(cancellationToken).ConfigureAwait(false);

        var resources = descriptions.Select(d => new Resource
        {
            Uri = d.UriPattern,
            Name = d.Name,
            Description = d.Description,
            MimeType = d.MimeType
        }).ToList();

        return new ListResourcesResult { Resources = resources };
    }

    /// <summary>
    /// Handle ReadResourceRequest for assembly metadata URIs.
    /// </summary>
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
