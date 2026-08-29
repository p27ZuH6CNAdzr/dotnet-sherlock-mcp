namespace Sherlock.MCP.Runtime.Resources;

using Sherlock.MCP.Runtime.Caching;
using Sherlock.MCP.Runtime.Inspection;
using Sherlock.MCP.Runtime.Resources.Handlers;

/// <summary>
/// Provides queryable assembly metadata via MCP Resources.
/// Supports resource URIs for types, members, references, and metadata.
/// </summary>
public class AssemblyResourceProvider : IResourceProvider
{
    private readonly TypesResourceHandler _typesHandler;
    private readonly MembersResourceHandler _membersHandler;
    private readonly ReferencesResourceHandler _referencesHandler;
    private readonly MetadataResourceHandler _metadataHandler;
    private readonly IToolResponseCache _cache;

    public AssemblyResourceProvider(
        IInspectionContextProvider contextProvider,
        IToolResponseCache cache)
    {
        _typesHandler = new TypesResourceHandler(contextProvider);
        _membersHandler = new MembersResourceHandler(contextProvider);
        _referencesHandler = new ReferencesResourceHandler(contextProvider);
        _metadataHandler = new MetadataResourceHandler(contextProvider);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc/>
    public async Task<ResourceContent?> GetResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Dispatch to appropriate handler
        ResourceContent? content = null;

        if (MetadataResourceHandler.CanHandle(uri))
            content = await _metadataHandler.HandleAsync(uri, cancellationToken).ConfigureAwait(false);
        else if (ReferencesResourceHandler.CanHandle(uri))
            content = await _referencesHandler.HandleAsync(uri, cancellationToken).ConfigureAwait(false);
        else if (MembersResourceHandler.CanHandle(uri))
            content = await _membersHandler.HandleAsync(uri, cancellationToken).ConfigureAwait(false);
        else if (TypesResourceHandler.CanHandle(uri))
            content = await _typesHandler.HandleAsync(uri, cancellationToken).ConfigureAwait(false);

        return content;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ResourceDescription>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var patterns = new List<ResourceDescription>
        {
            new(
                UriPattern: "assembly:///<path>/types",
                Name: "Types List",
                Description: "List all public types in an assembly with brief metadata (hierarchy, interfaces)",
                MimeType: "text/plain"),

            new(
                UriPattern: "assembly:///<path>/types/<Namespace.Type>",
                Name: "Type Detail",
                Description: "Get detailed metadata for a specific type (members summary, base type, interfaces)",
                MimeType: "text/plain"),

            new(
                UriPattern: "assembly:///<path>/types/<Namespace.Type>/members",
                Name: "Type Members",
                Description: "Get all public members of a type (methods, properties, fields, events with signatures)",
                MimeType: "text/plain"),

            new(
                UriPattern: "assembly:///<path>/metadata",
                Name: "Assembly Metadata",
                Description: "Get assembly identity, version, target framework, culture, and public key token",
                MimeType: "application/json"),

            new(
                UriPattern: "assembly:///<path>/references",
                Name: "Assembly References",
                Description: "Get all resolved assembly dependencies with versions and public key tokens",
                MimeType: "text/plain")
        };

        return Task.FromResult<IEnumerable<ResourceDescription>>(patterns);
    }
}
