using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Services;

public static class GitInfoReader
{
    public static GitManifest? TryRead(string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return null;
        }

        var gitDir = Path.Combine(repositoryRoot, ".git");
        if (!Directory.Exists(gitDir))
        {
            return null;
        }

        var headFile = Path.Combine(gitDir, "HEAD");
        if (!File.Exists(headFile))
        {
            return null;
        }

        try
        {
            var head = File.ReadAllText(headFile).Trim();
            if (head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                var reference = head[4..].Trim();
                var refPath = Path.Combine(gitDir, reference.Replace('/', Path.DirectorySeparatorChar));
                var commit = File.Exists(refPath) ? File.ReadAllText(refPath).Trim() : null;
                var branch = Path.GetFileName(reference);
                return new GitManifest { Commit = commit, Branch = branch };
            }

            return new GitManifest { Commit = head, Branch = "DETACHED" };
        }
        catch
        {
            return null;
        }
    }
}
