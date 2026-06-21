using Sherlock.MCP.Runtime.Inspection;

namespace Sherlock.MCP.Tests;

public class NuGetCacheProbeTests
{
    [Fact]
    public void TryParseCacheLayout_RecognizesNuGetLibPath()
    {
        using var cache = new TempCache();
        var dll = cache.AddPackageDll("pkga", "1.0.0", "net8.0", "PkgA.dll");

        var parsed = NuGetCacheProbe.TryParseCacheLayout(dll, out var tfm, out var packageId);

        Assert.True(parsed);
        Assert.Equal("net8.0", tfm);
        Assert.Equal("pkga", packageId);
    }

    [Fact]
    public void TryParseCacheLayout_RejectsPathOutsideCache()
    {
        using var cache = new TempCache();
        var outside = Path.Combine(Path.GetTempPath(), $"sherlock_outside_{Guid.NewGuid():N}.dll");

        var parsed = NuGetCacheProbe.TryParseCacheLayout(outside, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void EnumerateCandidateDependencyDlls_FindsSibling_ExcludesSelf()
    {
        using var cache = new TempCache();
        var primary = cache.AddPackageDll("pkga", "1.0.0", "net8.0", "PkgA.dll");
        var dependency = cache.AddPackageDll("depb", "2.0.0", "net8.0", "DepB.dll");

        var candidates = NuGetCacheProbe.EnumerateCandidateDependencyDlls("net8.0", "pkga");

        Assert.Contains(candidates, c => string.Equals(c, dependency, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(candidates, c => string.Equals(c, primary, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnumerateCandidateDependencyDlls_FallsBackToCompatibleTfm()
    {
        using var cache = new TempCache();
        cache.AddPackageDll("pkga", "1.0.0", "net8.0", "PkgA.dll");
        var dependency = cache.AddPackageDll("depb", "2.0.0", "netstandard2.0", "DepB.dll");

        var candidates = NuGetCacheProbe.EnumerateCandidateDependencyDlls("net8.0", "pkga");

        Assert.Contains(candidates, c => string.Equals(c, dependency, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnumerateCandidateDependencyDlls_AcceptsLowerNetTfm()
    {
        using var cache = new TempCache();
        cache.AddPackageDll("pkga", "1.0.0", "net8.0", "PkgA.dll");
        var dependency = cache.AddPackageDll("depb", "2.0.0", "net6.0", "DepB.dll");

        var candidates = NuGetCacheProbe.EnumerateCandidateDependencyDlls("net8.0", "pkga");

        Assert.Contains(candidates, c => string.Equals(c, dependency, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnumerateCandidateDependencyDlls_PrefersNearestCompatibleTfm()
    {
        using var cache = new TempCache();
        cache.AddPackageDll("pkga", "1.0.0", "net8.0", "PkgA.dll");
        cache.AddPackageDll("depb", "2.0.0", "net6.0", "DepB.dll");
        var preferred = cache.AddPackageDll("depb", "2.0.0", "net8.0", "DepB.dll");

        var candidates = NuGetCacheProbe.EnumerateCandidateDependencyDlls("net8.0", "pkga");

        Assert.Contains(preferred, candidates);
        Assert.DoesNotContain(candidates, c => c.Contains($"{Path.DirectorySeparatorChar}net6.0{Path.DirectorySeparatorChar}"));
    }

    private sealed class TempCache : IDisposable
    {
        private static readonly object EnvLock = new();
        private readonly string? _previous;

        public TempCache()
        {
            Monitor.Enter(EnvLock);
            try
            {
                Root = Path.Combine(Path.GetTempPath(), $"sherlock_cache_{Guid.NewGuid():N}");
                Directory.CreateDirectory(Root);
                _previous = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", Root);
            }
            catch
            {
                Monitor.Exit(EnvLock);
                throw;
            }
        }

        public string Root { get; } = "";

        public string AddPackageDll(string packageId, string version, string tfm, string fileName)
        {
            var dir = Path.Combine(Root, packageId, version, "lib", tfm);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, "");
            return path;
        }

        public void Dispose()
        {
            try
            {
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", _previous);
                try { Directory.Delete(Root, recursive: true); } catch { }
            }
            finally
            {
                Monitor.Exit(EnvLock);
            }
        }
    }
}
