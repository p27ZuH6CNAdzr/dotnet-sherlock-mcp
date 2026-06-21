using System.Reflection;

namespace Sherlock.MCP.Runtime.Inspection;

internal static class DependencyDiagnostics
{
    public static string[] ExtractUnresolved(ReflectionTypeLoadException ex)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loaderException in ex.LoaderExceptions)
        {
            var name = ExtractAssemblyName(loaderException);
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }
        return names.ToArray();
    }

    public static string? ExtractUnresolved(Exception ex) => ExtractAssemblyName(ex);

    private static string? ExtractAssemblyName(Exception? loaderException) =>
        loaderException switch
        {
            FileNotFoundException fnf => ToSimpleName(NameFrom(fnf.FileName, fnf.Message)),
            FileLoadException fle => ToSimpleName(NameFrom(fle.FileName, fle.Message)),
            _ => null
        };

    private static string NameFrom(string? fileName, string? message)
    {
        if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
        if (string.IsNullOrWhiteSpace(message)) return "";
        var start = message.IndexOf('\'');
        var end = start >= 0 ? message.IndexOf('\'', start + 1) : -1;
        return start >= 0 && end > start ? message[(start + 1)..end] : "";
    }

    private static string? ToSimpleName(string assemblyDisplayName)
    {
        if (string.IsNullOrWhiteSpace(assemblyDisplayName)) return null;
        var comma = assemblyDisplayName.IndexOf(',');
        var name = comma >= 0 ? assemblyDisplayName[..comma] : assemblyDisplayName;
        return name.Trim();
    }
}
