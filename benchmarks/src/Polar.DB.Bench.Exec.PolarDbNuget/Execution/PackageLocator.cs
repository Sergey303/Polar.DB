namespace Polar.DB.Bench.Exec.PolarDbNuget.Execution;

internal static class PackageLocator
{
    public static string ResolvePolarDll(
        string? explicitDllPath,
        string packageId,
        string? packageVersion,
        string tfm,
        string? nugetCachePath)
    {
        if (!string.IsNullOrWhiteSpace(explicitDllPath))
        {
            var full = Path.GetFullPath(explicitDllPath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException("Polar.DB DLL was not found.", full);
            }

            return full;
        }

        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new ArgumentException("Package version is required when --polar-dll is not specified.");
        }

        var cache = ResolveNugetCache(nugetCachePath);
        var packageFolder = Path.Combine(cache, packageId.ToLowerInvariant(), packageVersion);
        if (!Directory.Exists(packageFolder))
        {
            throw new DirectoryNotFoundException($"NuGet package folder was not found: {packageFolder}");
        }

        var exact = Path.Combine(packageFolder, "lib", tfm, "Polar.DB.dll");
        if (File.Exists(exact))
        {
            return Path.GetFullPath(exact);
        }

        var candidates = Directory.EnumerateFiles(packageFolder, "Polar.DB.dll", SearchOption.AllDirectories)
            .OrderBy(path => ScoreCandidate(path, tfm))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new FileNotFoundException($"Polar.DB.dll was not found under NuGet package folder: {packageFolder}");
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static string ResolveNugetCache(string? nugetCachePath)
    {
        if (!string.IsNullOrWhiteSpace(nugetCachePath))
        {
            return Path.GetFullPath(nugetCachePath);
        }

        var env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException("Cannot resolve user profile folder for NuGet cache.");
        }

        return Path.Combine(home, ".nuget", "packages");
    }

    private static int ScoreCandidate(string path, string preferredTfm)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains($"/lib/{preferredTfm}/", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalized.Contains("/lib/netstandard2.1/", StringComparison.OrdinalIgnoreCase)) return 1;
        if (normalized.Contains("/lib/netstandard2.0/", StringComparison.OrdinalIgnoreCase)) return 2;
        if (normalized.Contains("/lib/net8.0/", StringComparison.OrdinalIgnoreCase)) return 3;
        if (normalized.Contains("/lib/net7.0/", StringComparison.OrdinalIgnoreCase)) return 4;
        return 100;
    }
}
