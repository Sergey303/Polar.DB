using System.Diagnostics;

namespace PolarDbBenchmarks;

/// <summary>
/// Per-iteration timing: each stage has one paired sample, not an aggregate of
/// independent runs. Instrumentation overhead is included in the total.
/// </summary>
internal sealed class ReopenStageRecorder
{
    private readonly Dictionary<string, List<double>> _stages = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<double>> Stages =>
        _stages.ToDictionary(item => item.Key, item => (IReadOnlyList<double>)item.Value);

    public List<double> Measure(int warmup, int measured, string prefix,
        Action<Action<string, double>> operation)
    {
        var totals = new List<double>(measured);
        HashSet<string>? expected = null;
        for (int i = -warmup; i < measured; i++)
        {
            var current = new Dictionary<string, double>(StringComparer.Ordinal);
            void Observe(string name, double milliseconds)
            {
                if (milliseconds < 0 || !current.TryAdd(prefix + "/" + name, milliseconds))
                    throw new InvalidDataException("Invalid or duplicate reopen stage: " + name);
            }

            var timer = Stopwatch.StartNew();
            operation(Observe);
            timer.Stop();
            if (i < 0) continue;
            var keys = current.Keys.ToHashSet(StringComparer.Ordinal);
            if (expected == null) expected = keys;
            else if (!expected.SetEquals(keys))
                throw new InvalidDataException("Stage set differs between measured reopen iterations.");
            if (current.Values.Sum() > timer.Elapsed.TotalMilliseconds + 0.05)
                throw new InvalidDataException("Overlapping reopen stage timings.");
            totals.Add(timer.Elapsed.TotalMilliseconds);
            foreach (var (name, milliseconds) in current)
            {
                if (!_stages.TryGetValue(name, out var samples))
                    _stages[name] = samples = new List<double>(measured);
                samples.Add(milliseconds);
            }
        }
        return totals;
    }

    public static void Time(Action<string, double>? observer, string stage, Action operation)
    {
        if (observer == null) { operation(); return; }
        var started = Stopwatch.GetTimestamp();
        operation();
        observer(stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    public static T Time<T>(Action<string, double>? observer, string stage, Func<T> operation)
    {
        if (observer == null) return operation();
        var started = Stopwatch.GetTimestamp();
        T value = operation();
        observer(stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return value;
    }

    // File-handle baseline: intentionally distinct from the engine's constructor.
    // Does not parse checkpoints, instantiate indexes or perform a database query.
    public static void ProbeRawFileHandles(string[] paths)
    {
        var streams = new List<FileStream>(paths.Length);
        try
        {
            foreach (string path in paths)
                streams.Add(new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
        }
        finally
        {
            foreach (var stream in streams) stream.Dispose();
        }
    }
}
