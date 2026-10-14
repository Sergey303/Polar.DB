using System.Diagnostics;
using Polar.DB;

int rows = args.Length > 0 ? int.Parse(args[0]) : 50_000;
int repetitions = args.Length > 1 ? int.Parse(args[1]) : 5;
if (rows <= 0 || rows > 1_000_000 || repetitions < 2 || repetitions > 20)
    throw new ArgumentOutOfRangeException(nameof(args), "Rows: 1..1,000,000; repetitions: 2..20.");

var values = new Guid[rows];
for (int i = 0; i < rows; i++)
    values[i] = new Guid(i, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);

string root = Path.Combine(Path.GetTempPath(), "polar-guid-storage-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Console.WriteLine("format,rows,repeat,write_ms,read_ms,file_bytes");
try
{
    for (int repeat = 0; repeat < repetitions; repeat++)
    {
        // Reverse order on alternate repetitions to reduce order effects.
        bool nativeFirst = repeat % 2 == 0;
        Measure(nativeFirst, repeat);
        Measure(!nativeFirst, repeat);
    }
}
finally
{
    Directory.Delete(root, recursive: true);
}

void Measure(bool native, int repeat)
{
    var type = new PType(native ? PTypeEnumeration.guid : PTypeEnumeration.sstring);
    string path = Path.Combine(root, $"r{repeat}-{(native ? "native" : "legacy")}.bin");
    var timer = Stopwatch.StartNew();
    using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
    using (var sequence = new UniversalSequenceBase(type, stream))
    {
        foreach (Guid value in values)
            sequence.AppendElement(native ? value : value.ToString("N"));
        sequence.Flush();
    }
    timer.Stop();
    double writeMs = timer.Elapsed.TotalMilliseconds;
    long bytes = new FileInfo(path).Length;

    timer.Restart();
    using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
    using (var sequence = new UniversalSequenceBase(type, stream))
    {
        int index = 0;
        foreach (object value in sequence.ElementValues())
        {
            Guid result = native ? (Guid)value : Guid.ParseExact((string)value, "N");
            if (index >= values.Length || result != values[index++])
                throw new InvalidDataException("Guid storage round-trip mismatch.");
        }
        if (index != rows)
            throw new InvalidDataException("Guid storage row-count mismatch.");
    }
    timer.Stop();

    if (bytes != sizeof(long) + (long)rows * (native ? 16 : 33))
        throw new InvalidDataException("Unexpected physical Guid file size.");
    Console.WriteLine($"{(native ? "native" : "legacy")},{rows},{repeat},{writeMs:F3},{timer.Elapsed.TotalMilliseconds:F3},{bytes}");
    File.Delete(path);
}
