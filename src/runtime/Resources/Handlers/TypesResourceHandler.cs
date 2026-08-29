namespace Sherlock.MCP.Runtime.Resources.Handlers;

using System.Globalization;
using System.Reflection;
using System.Text;
using Sherlock.MCP.Runtime.Inspection;

/// <summary>
/// Handles assembly://path/to/Lib.dll/types and assembly://path/to/Lib.dll/types/Namespace.Type URIs.
/// </summary>
public class TypesResourceHandler : ResourceHandlerBase
{
    private static readonly string[] TypesSeparator = ["/types"];

    public TypesResourceHandler(IInspectionContextProvider contextProvider) : base(contextProvider)
    {
    }

    /// <summary>
    /// Match URIs like: assembly:///path/to/Lib.dll/types or assembly:///path/to/Lib.dll/types/Namespace.Type
    /// </summary>
    public static bool CanHandle(string uri) =>
        uri.StartsWith("assembly://", StringComparison.OrdinalIgnoreCase) &&
        uri.Contains("/types", StringComparison.OrdinalIgnoreCase);

    public Task<ResourceContent?> HandleAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Parse: assembly:///path/to/Lib.dll/types or assembly:///path/to/Lib.dll/types/Namespace.Type
        var parts = uri.Split(TypesSeparator, StringSplitOptions.None);
        if (parts.Length < 1)
            return Task.FromResult<ResourceContent?>(null);

        var assemblyPart = parts[0].Replace("assembly://", string.Empty, StringComparison.OrdinalIgnoreCase).TrimStart('/');
        var typePart = parts.Length > 1 ? parts[1].TrimStart('/') : null;

        var assembly = GetAssembly(assemblyPart);
        if (assembly is null)
            return Task.FromResult<ResourceContent?>(null);

        var content = typePart is null
            ? RenderTypeList(assembly)
            : RenderTypeDetail(assembly, typePart);

        var resource = new ResourceContent(
            Uri: uri,
            Name: typePart is null ? $"Types in {Path.GetFileName(assemblyPart)}" : $"Type {typePart}",
            Description: typePart is null ? "Public types with brief metadata" : "Detailed type metadata",
            MimeType: "text/plain",
            Content: content); // 1 hour TTL

        return Task.FromResult<ResourceContent?>(resource);
    }

    private static string RenderTypeList(Assembly assembly)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Types in {assembly.GetName().Name}");
        sb.AppendLine();

        var types = assembly.GetExportedTypes().OrderBy(t => t.FullName).ToList();
        if (types.Count == 0)
        {
            sb.AppendLine("(No public types)");
            return sb.ToString();
        }

        foreach (var type in types)
        {
            var kind = type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.IsAbstract ? "abstract class" : "class";
            sb.Append(CultureInfo.InvariantCulture, $"{type.FullName} ({kind})");

            var baseType = type.BaseType;
            if (baseType is not null && baseType != typeof(object) && baseType != typeof(ValueType))
            {
                sb.Append(CultureInfo.InvariantCulture, $" : {baseType.Name}");
            }

            var interfaces = type.GetInterfaces().Where(i => i != baseType?.GetInterfaces().FirstOrDefault()).ToList();
            if (interfaces.Count > 0)
            {
                if (baseType is not null && baseType != typeof(object))
                    sb.Append(", ");
                else
                    sb.Append(" : ");

                sb.Append(string.Join(", ", interfaces.Select(i => i.Name)));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderTypeDetail(Assembly assembly, string typeName)
    {
        var type = assembly.GetExportedTypes().FirstOrDefault(t =>
            t.FullName?.Equals(typeName, StringComparison.OrdinalIgnoreCase) ?? false);

        if (type is null)
            return $"Type '{typeName}' not found in {assembly.GetName().Name}.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {type.FullName}");
        sb.AppendLine();

        // Kind
        var kind = type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.IsAbstract ? "abstract class" : "class";
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Kind:** {kind}");

        // Base type
        if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Base:** {type.BaseType.FullName}");
        }

        // Interfaces
        var interfaces = type.GetInterfaces().ToList();
        if (interfaces.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Interfaces:** {string.Join(", ", interfaces.Select(i => i.FullName))}");
        }

        // Summary
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Members:** {type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length}");

        // Public methods (summary)
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName)
            .OrderBy(m => m.Name)
            .ToList();

        if (methods.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Methods:**");
            foreach (var method in methods.Take(10))
            {
                var @params = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {method.ReturnType.Name} {method.Name}({@params})");
            }

            if (methods.Count > 10)
                sb.AppendLine(CultureInfo.InvariantCulture, $"  ... and {methods.Count - 10} more");
        }

        return sb.ToString();
    }
}
