namespace Sherlock.MCP.Runtime.Inspection;

internal static class NuGetCacheProbe
{
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static string GetCacheRoot()
    {
        var overridePath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".nuget", "packages");
    }

    public static bool TryParseCacheLayout(string fullAssemblyPath, out string consumingTfm, out string packageId)
    {
        consumingTfm = "";
        packageId = "";

        var cacheRoot = GetCacheRoot();
        if (string.IsNullOrWhiteSpace(cacheRoot) || !Directory.Exists(cacheRoot)) return false;

        var fullCacheRoot = Path.GetFullPath(cacheRoot);
        var normalized = Path.GetFullPath(fullAssemblyPath);
        if (!normalized.StartsWith(EnsureTrailingSeparator(fullCacheRoot), PathComparison)) return false;

        var relative = Path.GetRelativePath(fullCacheRoot, normalized);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length < 5) return false;

        var folderKind = segments[2];
        if (!folderKind.Equals("lib", StringComparison.OrdinalIgnoreCase) && !folderKind.Equals("ref", StringComparison.OrdinalIgnoreCase))
            return false;

        packageId = segments[0];
        consumingTfm = segments[3];
        return true;
    }

    public static List<string> EnumerateCandidateDependencyDlls(string consumingTfm, string excludePackageId)
    {
        var candidates = new List<string>();
        var cacheRoot = GetCacheRoot();
        if (string.IsNullOrWhiteSpace(cacheRoot) || !Directory.Exists(cacheRoot)) return candidates;

        foreach (var packageDir in SafeEnumerateDirectories(cacheRoot))
        {
            var packageName = Path.GetFileName(packageDir);
            if (packageName.Equals(excludePackageId, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var version = PickHighestVersion(SafeEnumerateDirectories(packageDir).Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToArray());
                if (version is null) continue;

                var libDir = Path.Combine(packageDir, version, "lib");
                if (!Directory.Exists(libDir)) continue;

                var tfms = SafeEnumerateDirectories(libDir).Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToArray();
                var tfm = PickCompatibleTfm(tfms, consumingTfm);
                if (tfm is null) continue;

                candidates.AddRange(Directory.EnumerateFiles(Path.Combine(libDir, tfm), "*.dll", SearchOption.TopDirectoryOnly));
            }
            catch { }
        }
        return candidates;
    }

    private static string[] SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.GetDirectories(directory);
        }
        catch
        {
            return [];
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static string? PickHighestVersion(string[] versions)
    {
        if (versions.Length == 0) return null;
        var parsed = versions
            .Select(v => (raw: v, parsed: TryParseVersion(v), isStable: !v.Contains('-')))
            .Where(p => p.parsed is not null)
            .ToArray();
        if (parsed.Length > 0)
            return parsed
                .OrderByDescending(p => p.isStable)
                .ThenByDescending(p => p.parsed!)
                .ThenByDescending(p => p.raw, StringComparer.OrdinalIgnoreCase)
                .First().raw;
        return versions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).First();
    }

    private static Version? TryParseVersion(string raw)
    {
        var core = raw.AsSpan();
        var cut = core.IndexOfAny('-', '+');
        if (cut >= 0) core = core[..cut];
        return Version.TryParse(core, out var v) ? v : null;
    }

    private static string? PickCompatibleTfm(string[] availableTfms, string requested)
    {
        var exact = availableTfms.FirstOrDefault(t => t.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        return availableTfms
            .Where(t => IsCompatibleFramework(t, requested))
            .OrderByDescending(t => t, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsCompatibleFramework(string availableFramework, string targetFramework)
    {
        if (availableFramework.Equals(targetFramework, StringComparison.OrdinalIgnoreCase)) return true;
        if (targetFramework.StartsWith("net", StringComparison.Ordinal) && !targetFramework.Contains("framework"))
        {
            if (availableFramework.Equals("netstandard2.0", StringComparison.OrdinalIgnoreCase) ||
                availableFramework.Equals("netstandard2.1", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
