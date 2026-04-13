using System.Diagnostics;
using Xunit;

namespace Polar.DB.Tests;

internal static class StorageCorruptionHelpers
{
    public static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "PolarDbTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteDirectoryQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup for tests
        }
    }

    public static USequence CreateIntegerSequence(
        string tempDir,
        string? stateFileName = null,
        bool optimise = true,
        FileShare fileShare = FileShare.ReadWrite,
        Func<int, int>? hashOfKey = null)
    {
        string[] files =
        {
            Path.Combine(tempDir, "sequence.bin"),
            Path.Combine(tempDir, "hkeys.bin"),
            Path.Combine(tempDir, "offsets.bin")
        };

        int index = 0;
        Func<Stream> streamGen = () => new FileStream(
            files[index++],
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            fileShare);

        return new USequence(
            new PType(PTypeEnumeration.integer),
            stateFileName,
            streamGen,
            _ => false,
            value => (int)value,
            key => hashOfKey?.Invoke((int)key) ?? (int)key,
            optimise);
    }

    public static UniversalSequenceBase CreateIntegerUniversalSequence(string path)
    {
        return new UniversalSequenceBase(
            new PType(PTypeEnumeration.integer),
            new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
    }

    public static UniversalSequenceBase CreateStringUniversalSequence(string path)
    {
        return new UniversalSequenceBase(
            new PType(PTypeEnumeration.sstring),
            new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
    }

    public static (long Count, long AppendOffset) ReadState(string statePath)
    {
        using var fs = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);
        return (br.ReadInt64(), br.ReadInt64());
    }

    public static void WriteState(string statePath, long count, long appendOffset)
    {
        string? dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var fs = new FileStream(statePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var bw = new BinaryWriter(fs);
        bw.Write(count);
        bw.Write(appendOffset);
    }

    public static void WriteTooShortState(string statePath)
    {
        string? dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(statePath, new byte[] { 1, 2, 3, 4 });
    }

    public static void CorruptHeaderCount(string sequencePath, long count)
    {
        using var fs = new FileStream(sequencePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var bw = new BinaryWriter(fs);
        fs.Position = 0L;
        bw.Write(count);
        bw.Flush();
    }

    public static void AppendBytes(string path, byte[] bytes)
    {
        using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        fs.Position = fs.Length;
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush();
    }

    public static void WriteFixedSequenceBytes(Stream stream, IReadOnlyList<int> values, long declaredCount, byte[]? trailingBytes = null)
    {
        stream.SetLength(0L);
        stream.Position = 0L;

        using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        bw.Write(declaredCount);
        foreach (int value in values)
            bw.Write(value);

        if (trailingBytes is { Length: > 0 })
            bw.Write(trailingBytes);

        bw.Flush();
        stream.Position = 0L;
    }

    public static byte[] BuildVariableStringSequenceBytes(params string[] values)
    {
        using var ms = new MemoryStream();
        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), ms);
        foreach (string value in values)
            sequence.AppendElement(value);
        sequence.Flush();
        return ms.ToArray();
    }

    public static byte[] ConcatBytes(byte[] left, byte[] right)
    {
        byte[] result = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, result, 0, left.Length);
        Buffer.BlockCopy(right, 0, result, left.Length, right.Length);
        return result;
    }

    public static long ReadBudgetMs(string envVarName, long fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(envVarName);
        return long.TryParse(raw, out long parsed) && parsed > 0 ? parsed : fallback;
    }

    public static void AssertDurationWithin(Stopwatch sw, long budgetMs, string scenario)
    {
        Assert.True(
            sw.ElapsedMilliseconds <= budgetMs,
            $"{scenario} exceeded budget. Expected <= {budgetMs} ms, actual: {sw.ElapsedMilliseconds} ms.");
    }
}
