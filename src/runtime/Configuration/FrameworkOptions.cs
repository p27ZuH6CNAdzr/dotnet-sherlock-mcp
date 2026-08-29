namespace Sherlock.MCP.Runtime.Configuration;

/// <summary>
/// Configurable target framework selection for assembly analysis.
/// Allows users to specify which .NET versions to analyze (e.g., net8.0, net9.0, net10.0, net11.0).
/// Default targets net10.0 and net11.0 (modern stack), but can be customized via CLI.
/// </summary>
public class FrameworkOptions
{
    private readonly string[] _targetFrameworks;

    public FrameworkOptions(string[]? targetFrameworks = null)
    {
        // Default to modern stack (LTS + latest STS)
        _targetFrameworks = targetFrameworks?.Length > 0
            ? targetFrameworks
            : new[] { "net10.0", "net11.0" };
    }

    /// <summary>
    /// Gets the list of target frameworks for this instance.
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks => _targetFrameworks;

    /// <summary>
    /// Checks if a specific framework is supported by this instance.
    /// </summary>
    public bool IsFrameworkSupported(string framework) =>
        _targetFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a comma-separated string of supported frameworks for display in MCP instructions.
    /// </summary>
    public string SupportedFrameworksDisplay => string.Join(", ", _targetFrameworks);
}
