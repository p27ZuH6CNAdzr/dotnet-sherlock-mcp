namespace Sherlock.MCP.Runtime.Prompts;

/// <summary>
/// Provides reusable, parameterized message templates for common analysis patterns.
/// Prompts are discoverable workflows that help agents perform standard Sherlock operations.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Get a specific prompt by name.
    /// </summary>
    /// <param name="name">Prompt name (e.g., "api-surface-analysis")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt definition with metadata and rendering options</returns>
    Task<PromptDefinition?> GetPromptAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available prompts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All prompt definitions</returns>
    Task<IEnumerable<PromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Render a prompt with specific arguments to get the message template.
    /// </summary>
    /// <param name="name">Prompt name</param>
    /// <param name="arguments">Argument values (parameter name -> value)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered prompt message</returns>
    Task<string> RenderPromptAsync(
        string name,
        Dictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata and definition of a reusable prompt.
/// </summary>
public record PromptDefinition(
    string Name,
    string Description,
    IReadOnlyList<PromptArgument> Arguments);

/// <summary>
/// Argument parameter for a prompt template.
/// </summary>
public record PromptArgument(
    string Name,
    string Description,
    bool Required = true);
