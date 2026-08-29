namespace Sherlock.MCP.Runtime.Resources.Handlers;

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Sherlock.MCP.Runtime.Inspection;

/// <summary>
/// Base class for resource handlers with common utilities.
/// </summary>
public abstract class ResourceHandlerBase
{
    protected IInspectionContextProvider ContextProvider { get; }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    protected ResourceHandlerBase(IInspectionContextProvider contextProvider)
    {
        ContextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
    }

    /// <summary>
    /// Get an assembly from the context provider.
    /// </summary>
    protected Assembly? GetAssembly(string assemblyPath)
    {
        try
        {
            var lease = ContextProvider.Acquire(assemblyPath);
            using (lease)
            {
                return lease.Context.Assembly;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Format a public key token as hex string.
    /// </summary>
    protected static string FormatPublicKeyToken(byte[]? token)
    {
        if (token is null || token.Length == 0)
            return "(none)";

        return string.Concat(token.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Get serializer options for JSON output.
    /// </summary>
    protected static JsonSerializerOptions GetJsonOptions() => JsonOptions;

    /// <summary>
    /// Get the target framework of an assembly.
    /// </summary>
    protected static string GetTargetFramework(Assembly assembly)
    {
        var framework = assembly
            .GetCustomAttributes<System.Runtime.Versioning.TargetFrameworkAttribute>()
            .FirstOrDefault();

        if (framework?.FrameworkName is not null)
            return framework.FrameworkName;

        // Fallback based on assembly location
        var location = assembly.Location;
        return location switch
        {
            var l when l.Contains("net6.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v6.0",
            var l when l.Contains("net7.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v7.0",
            var l when l.Contains("net8.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v8.0",
            var l when l.Contains("net9.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v9.0",
            var l when l.Contains("net10.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v10.0",
            var l when l.Contains("net11.0", StringComparison.OrdinalIgnoreCase) => ".NETCoreApp,Version=v11.0",
            _ => "Unknown"
        };
    }
}
