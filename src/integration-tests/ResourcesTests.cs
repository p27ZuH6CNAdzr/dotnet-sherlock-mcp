namespace Sherlock.MCP.IntegrationTests;

using System.Reflection;
using Sherlock.MCP.Runtime;
using Sherlock.MCP.Runtime.Caching;
using Sherlock.MCP.Runtime.Inspection;
using Sherlock.MCP.Runtime.Resources;
using Xunit;

public class ResourcesTests
{
    private readonly IResourceProvider _resourceProvider;

    public ResourcesTests()
    {
        var options = new RuntimeOptions();
        var contextProvider = new SharedInspectionContextProvider(options);
        _resourceProvider = new AssemblyResourceProvider(contextProvider, new MockCache());
    }

    [Fact]
    public async Task ListResources_ReturnsPatterns()
    {
        // Act
        var resources = await _resourceProvider.ListResourcesAsync();

        // Assert
        var list = resources.ToList();
        Assert.NotEmpty(list);
        Assert.Contains(list, r => r.UriPattern.Contains("/types"));
        Assert.Contains(list, r => r.UriPattern.Contains("/metadata"));
        Assert.Contains(list, r => r.UriPattern.Contains("/references"));
        Assert.All(list, r => Assert.NotNull(r.Name));
        Assert.All(list, r => Assert.NotNull(r.Description));
    }

    [Fact]
    public async Task GetResource_InvalidUri_ReturnsNull()
    {
        // Arrange
        var uri = "assembly:///nonexistent/types";

        // Act
        var resource = await _resourceProvider.GetResourceAsync(uri);

        // Assert
        Assert.Null(resource);
    }

    [Fact]
    public async Task GetResource_UnsupportedUri_ReturnsNull()
    {
        // Arrange - not a recognized pattern
        var uri = "assembly:///path/to/lib.dll/unknown";

        // Act
        var resource = await _resourceProvider.GetResourceAsync(uri);

        // Assert
        Assert.Null(resource);
    }

    private class MockCache : IToolResponseCache
    {
        public bool TryGet(string key, out string? payload)
        {
            payload = null;
            return false;
        }

        public void Set(string key, string payload, TimeSpan ttl)
        {
        }
    }
}
