namespace Sherlock.MCP.Runtime.Resources.Handlers;

using System.Globalization;
using System.Reflection;
using System.Text;
using Sherlock.MCP.Runtime.Inspection;

/// <summary>
/// Handles assembly://path/to/Lib.dll/types/Namespace.Type/members URIs.
/// </summary>
public class MembersResourceHandler : ResourceHandlerBase
{
    private static readonly string[] TypesTypeSeparator = ["/types/"];
    private static readonly string[] MembersSeparator = ["/members"];
    private static readonly BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    public MembersResourceHandler(IInspectionContextProvider contextProvider) : base(contextProvider)
    {
    }

    /// <summary>
    /// Match URIs like: assembly:///path/to/Lib.dll/types/Namespace.Type/members
    /// </summary>
    public static bool CanHandle(string uri) =>
        uri.StartsWith("assembly://", StringComparison.OrdinalIgnoreCase) &&
        uri.Contains("/types/", StringComparison.OrdinalIgnoreCase) &&
        uri.EndsWith("/members", StringComparison.OrdinalIgnoreCase);

    public Task<ResourceContent?> HandleAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Parse: assembly:///path/to/Lib.dll/types/Namespace.Type/members
        if (!ParseUri(uri, out var assemblyPath, out var typeName))
            return Task.FromResult<ResourceContent?>(null);

        var assembly = GetAssembly(assemblyPath);
        if (assembly is null)
            return Task.FromResult<ResourceContent?>(null);

        var type = assembly.GetExportedTypes().FirstOrDefault(t =>
            t.FullName?.Equals(typeName, StringComparison.OrdinalIgnoreCase) ?? false);

        if (type is null)
            return Task.FromResult<ResourceContent?>(null);

        var content = RenderMembers(type);

        var resource = new ResourceContent(
            Uri: uri,
            Name: $"Members of {typeName}",
            Description: "Methods, properties, fields, and events with signatures",
            MimeType: "text/plain",
            Content: content); // 1 hour TTL

        return Task.FromResult<ResourceContent?>(resource);
    }

    private static bool ParseUri(string uri, out string assemblyPath, out string typeName)
    {
        assemblyPath = string.Empty;
        typeName = string.Empty;

        try
        {
            // assembly:///path/to/Lib.dll/types/Namespace.Type/members
            var normalized = uri.Replace("assembly://", string.Empty, StringComparison.OrdinalIgnoreCase);
            var parts = normalized.Split(TypesTypeSeparator, StringSplitOptions.None);

            if (parts.Length != 2)
                return false;

            assemblyPath = "/" + parts[0];
            var typeAndMembers = parts[1].Split(MembersSeparator, StringSplitOptions.None);

            if (typeAndMembers.Length != 2)
                return false;

            typeName = typeAndMembers[0];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RenderMembers(Type type)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Members of {type.FullName}");
        sb.AppendLine();

        // Methods
        var methods = type.GetMethods(PublicMembers)
            .Where(m => !m.IsSpecialName)
            .OrderBy(m => m.Name)
            .ToList();

        if (methods.Count > 0)
        {
            sb.AppendLine("## Methods");
            sb.AppendLine();

            foreach (var method in methods)
            {
                var @params = string.Join(", ", method.GetParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}"));

                var returnType = method.ReturnType.Name;
                var modifiers = (method.IsStatic ? "static " : "") +
                               (method.IsAbstract ? "abstract " : "") +
                               (method.IsVirtual && !method.IsFinal ? "virtual " : "");

                sb.AppendLine(CultureInfo.InvariantCulture, $"  {modifiers}{returnType} {method.Name}({@params})");
            }

            sb.AppendLine();
        }

        // Properties
        var properties = type.GetProperties(PublicMembers)
            .OrderBy(p => p.Name)
            .ToList();

        if (properties.Count > 0)
        {
            sb.AppendLine("## Properties");
            sb.AppendLine();

            foreach (var prop in properties)
            {
                var getSet = string.Empty;
                if (prop.GetMethod?.IsPublic ?? false)
                    getSet += "get; ";
                if (prop.SetMethod?.IsPublic ?? false)
                    getSet += "set; ";

                var modifiers = (prop.GetMethod?.IsStatic ?? false) ? "static " : "";
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {modifiers}{prop.PropertyType.Name} {prop.Name} {{ {getSet}}}");
            }

            sb.AppendLine();
        }

        // Fields
        var fields = type.GetFields(PublicMembers)
            .OrderBy(f => f.Name)
            .ToList();

        if (fields.Count > 0)
        {
            sb.AppendLine("## Fields");
            sb.AppendLine();

            foreach (var field in fields)
            {
                var modifiers = (field.IsStatic ? "static " : "") +
                               (field.IsInitOnly ? "readonly " : "");

                sb.AppendLine(CultureInfo.InvariantCulture, $"  {modifiers}{field.FieldType.Name} {field.Name}");
            }

            sb.AppendLine();
        }

        // Events
        var events = type.GetEvents(PublicMembers)
            .OrderBy(e => e.Name)
            .ToList();

        if (events.Count > 0)
        {
            sb.AppendLine("## Events");
            sb.AppendLine();

            foreach (var @event in events)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {@event.EventHandlerType?.Name} {Escape(@event.Name)}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("event", "@event", StringComparison.Ordinal);
}
