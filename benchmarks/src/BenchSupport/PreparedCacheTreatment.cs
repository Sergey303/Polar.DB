using System.Runtime.InteropServices;

namespace PolarDbBenchmarks;

// Opt-in, Linux-only, advisory file-page eviction. Never modifies database bytes.
internal static class PreparedCacheTreatment
{
    private const int PosixFadvDontNeed = 4;
    private const long Stripe = 32L * 1024 * 1024;

    [DllImport("libc", EntryPoint = "posix_fadvise", SetLastError = false)]
    private static extern int PosixFadvise(int fd, long offset, long length, int advice);

    public static void Validate(string condition, string engine)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Controlled file cache requires Linux.");
        var polar = new[] { "none", "payload", "payload-half", "hashes", "offsets" };
        var sqlite = new[] { "none", "data", "data-half" };
        if (!(engine == "polar-db" ? polar : sqlite).Contains(condition, StringComparer.Ordinal))
            throw new ArgumentException("Invalid cache treatment for " + engine + ": " + condition);
    }

    public static (PreparedFileResidency.Snapshot Before, PreparedFileResidency.Snapshot After)
        ApplyAndVerify(string directory, string engine, string condition,
            IEnumerable<string> files)
    {
        var paths = files.ToArray();
        var before = PreparedFileResidency.Capture(directory, paths)!;
        AssertApplied("none", engine, before);
        Apply(directory, engine, condition);
        var after = PreparedFileResidency.Capture(directory, paths)!;
        AssertApplied(condition, engine, after);
        return (before, after);
    }

    public static void Apply(string directory, string engine, string condition)
    {
        Validate(condition, engine);
        if (condition == "none") return;
        var name = engine == "sqlite" ? "data.sqlite" : condition switch
        {
            "payload" or "payload-half" => "f0.bin",
            "hashes" => "f1.bin",
            "offsets" => "f2.bin",
            _ => throw new ArgumentException("Unsupported cache treatment")
        };
        var path = Path.Combine(directory, name);
        using var stream = File.OpenRead(path);
        if (condition.EndsWith("-half", StringComparison.Ordinal))
        {
            for (long offset = 0; offset < stream.Length; offset += Stripe * 2)
            {
                var remainingBlock = Math.Min(Stripe * 2, stream.Length - offset);
                Evict(stream, offset, (remainingBlock + 1) / 2);
            }
        }
        else Evict(stream, 0, 0); // length 0 means to EOF
    }

    private static void Evict(FileStream stream, long offset, long length)
    {
        var fd = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        var error = PosixFadvise(fd, offset, length, PosixFadvDontNeed);
        if (error != 0)
            throw new IOException($"posix_fadvise(DONTNEED) failed: errno={error} ({stream.Name})");
    }

    // Fail when an advisory eviction did not achieve the requested cache state.
    public static void AssertApplied(string condition, string engine,
        PreparedFileResidency.Snapshot snapshot)
    {
        var files = snapshot.Files.ToDictionary(f => f.Name, StringComparer.Ordinal);
        string? selected = null;
        double min = 0, max = .1;
        switch (condition)
        {
            case "none":
                if (snapshot.Files.Any(file => file.Fraction < .995))
                    throw new InvalidDataException("Fully resident control lost pages before batch.");
                return;
            case "data": selected = "data.sqlite"; break;
            case "data-half": selected = "data.sqlite"; min = .35; max = .65; break;
            case "payload": selected = "f0.bin"; break;
            case "payload-half": selected = "f0.bin"; min = .35; max = .65; break;
            case "hashes": selected = "f1.bin"; break;
            case "offsets": selected = "f2.bin"; break;
            default: throw new ArgumentException("Unknown cache treatment.");
        }
        if (!files.TryGetValue(selected, out var entry) || entry.Fraction < min || entry.Fraction > max)
            throw new InvalidDataException($"Treatment {condition} residency out of range: {entry?.Fraction}");
        if (engine == "polar-db")
        {
            if (selected is not "f0.bin" && files["f0.bin"].Fraction < .995)
                throw new InvalidDataException("Payload unexpectedly evicted during index treatment.");
            foreach (var index in new[] { "f1.bin", "f2.bin" })
            {
                if (index == selected) continue;
                if (files[index].Fraction < .995)
                    throw new InvalidDataException($"Unexpected index eviction: {index}={files[index].Fraction}");
            }
        }
    }
}
