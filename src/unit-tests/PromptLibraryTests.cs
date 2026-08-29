namespace Sherlock.MCP.Tests;

using Sherlock.MCP.Runtime.Prompts;
using Xunit;

public class PromptLibraryTests
{
    private readonly IPromptProvider _library = new PromptLibrary();

    [Fact]
    public async Task ListPrompts_ReturnsAllPrompts()
    {
        // Act
        var prompts = await _library.ListPromptsAsync();

        // Assert
        var list = prompts.ToList();
        Assert.Equal(5, list.Count);
        Assert.Contains(list, p => p.Name == "api-surface-analysis");
        Assert.Contains(list, p => p.Name == "type-hierarchy-trace");
        Assert.Contains(list, p => p.Name == "method-call-graph");
        Assert.Contains(list, p => p.Name == "dependency-inventory");
        Assert.Contains(list, p => p.Name == "breaking-change-detection");
    }

    [Fact]
    public async Task GetPrompt_WithValidName_ReturnsPrompt()
    {
        // Act
        var prompt = await _library.GetPromptAsync("api-surface-analysis");

        // Assert
        Assert.NotNull(prompt);
        Assert.Equal("api-surface-analysis", prompt.Name);
        Assert.NotEmpty(prompt.Description);
        Assert.Single(prompt.Arguments);
    }

    [Fact]
    public async Task GetPrompt_WithInvalidName_ReturnsNull()
    {
        // Act
        var prompt = await _library.GetPromptAsync("nonexistent-prompt");

        // Assert
        Assert.Null(prompt);
    }

    [Fact]
    public async Task RenderPrompt_ApiSurfaceAnalysis_IncludesAssemblyPath()
    {
        // Arrange
        var arguments = new Dictionary<string, string> { { "assemblyPath", "/path/to/MyLib.dll" } };

        // Act
        var message = await _library.RenderPromptAsync("api-surface-analysis", arguments);

        // Assert
        Assert.Contains("/path/to/MyLib.dll", message);
        Assert.Contains("public API surface", message);
        Assert.Contains("get_types_from_assembly", message);
    }

    [Fact]
    public async Task RenderPrompt_TypeHierarchyTrace_IncludesTypeAndAssembly()
    {
        // Arrange
        var arguments = new Dictionary<string, string>
        {
            { "assemblyPath", "/path/to/MyLib.dll" },
            { "typeName", "System.Collections.Generic.IEnumerable" }
        };

        // Act
        var message = await _library.RenderPromptAsync("type-hierarchy-trace", arguments);

        // Assert
        Assert.Contains("System.Collections.Generic.IEnumerable", message);
        Assert.Contains("/path/to/MyLib.dll", message);
        Assert.Contains("get_type_hierarchy", message);
    }

    [Fact]
    public async Task RenderPrompt_MethodCallGraph_IncludesMethodTypeAndAssembly()
    {
        // Arrange
        var arguments = new Dictionary<string, string>
        {
            { "assemblyPath", "/path/to/MyLib.dll" },
            { "typeName", "MyLib.MyClass" },
            { "methodName", "DoSomething" }
        };

        // Act
        var message = await _library.RenderPromptAsync("method-call-graph", arguments);

        // Assert
        Assert.Contains("DoSomething", message);
        Assert.Contains("MyLib.MyClass", message);
        Assert.Contains("/path/to/MyLib.dll", message);
        Assert.Contains("get_method_calls", message);
    }

    [Fact]
    public async Task RenderPrompt_DependencyInventory_IncludesAssemblyPath()
    {
        // Arrange
        var arguments = new Dictionary<string, string> { { "assemblyPath", "/path/to/MyLib.dll" } };

        // Act
        var message = await _library.RenderPromptAsync("dependency-inventory", arguments);

        // Assert
        Assert.Contains("/path/to/MyLib.dll", message);
        Assert.Contains("inventory", message);
        Assert.Contains("get_assembly_info", message);
    }

    [Fact]
    public async Task RenderPrompt_BreakingChangeDetection_IncludesBothVersions()
    {
        // Arrange
        var arguments = new Dictionary<string, string>
        {
            { "oldAssemblyPath", "/path/to/MyLib.1.0.dll" },
            { "newAssemblyPath", "/path/to/MyLib.2.0.dll" }
        };

        // Act
        var message = await _library.RenderPromptAsync("breaking-change-detection", arguments);

        // Assert
        Assert.Contains("/path/to/MyLib.1.0.dll", message);
        Assert.Contains("/path/to/MyLib.2.0.dll", message);
        Assert.Contains("breaking changes", message);
        Assert.Contains("get_types_from_assembly", message);
    }

    [Fact]
    public async Task RenderPrompt_MissingRequiredArgument_Throws()
    {
        // Arrange - missing "assemblyPath"
        var arguments = new Dictionary<string, string>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _library.RenderPromptAsync("api-surface-analysis", arguments));

        Assert.Contains("assemblyPath", ex.Message);
    }

    [Fact]
    public async Task RenderPrompt_WithCaseInsensitiveName_Works()
    {
        // Arrange
        var arguments = new Dictionary<string, string> { { "assemblyPath", "/path/to/MyLib.dll" } };

        // Act
        var message = await _library.RenderPromptAsync("API-SURFACE-ANALYSIS", arguments);

        // Assert
        Assert.NotEmpty(message);
        Assert.Contains("/path/to/MyLib.dll", message);
    }

    [Fact]
    public async Task RenderPrompt_InvalidPromptName_Throws()
    {
        // Arrange
        var arguments = new Dictionary<string, string> { { "assemblyPath", "/path/to/MyLib.dll" } };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _library.RenderPromptAsync("nonexistent", arguments));

        Assert.Contains("nonexistent", ex.Message);
    }
}
