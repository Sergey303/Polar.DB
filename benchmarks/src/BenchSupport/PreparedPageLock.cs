using System.Runtime.InteropServices;

namespace PolarDbBenchmarks;

// Linux-only experiment fixture: pin sealed file pages, not application heap.
// Each mapping stays alive until the independent query measurement finishes.
internal sealed class PreparedPageLock : IDisposable
{
    private const int ProtRead = 1;
    private const int MapShared = 1;
    private static readonly nint MapFailed = new(-1);
    private readonly List<MappedFile> _files = new();
    private bool _disposed;

    [DllImport("libc", SetLastError = true)]
    private static extern nint mmap(nint address, nuint length, int protection,
        int flags, int fd, long offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int mlock(nint address, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int munlock(nint address, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(nint address, nuint length);

    private sealed class MappedFile
    {
        public string Name { get; init; } = string.Empty;
        public nint Address { get; init; }
        public nuint Length { get; init; }
        public bool Locked { get; set; }
    }

    public long LockedBytes => _files.Where(f => f.Locked).Sum(f => (long)f.Length);
    public long InitiallyLockedBytes { get; private set; }

    public PreparedPageLock(string directory, IEnumerable<string> fileNames)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Pinned cache experiments require Linux.");
        try
        {
            foreach (var name in fileNames)
            {
                using var input = File.OpenRead(Path.Combine(directory, name));
                if (input.Length == 0) throw new InvalidDataException("Empty sealed file: " + name);
                var size = (nuint)input.Length;
                var fd = checked((int)input.SafeFileHandle.DangerousGetHandle());
                var address = mmap(0, size, ProtRead, MapShared, fd, 0);
                if (address == MapFailed)
                    throw new IOException($"mmap({name}) failed, errno={Marshal.GetLastPInvokeError()}");
                var file = new MappedFile { Name = name, Address = address, Length = size };
                _files.Add(file); // Register before locking so partial setup is always cleaned up.
                if (mlock(address, size) != 0)
                    throw new IOException($"mlock({name}, {input.Length} B) failed, " +
                        $"errno={Marshal.GetLastPInvokeError()}. Need sufficient RAM and " +
                        "--ulimit memlock=-1:-1 (CAP_IPC_LOCK may be necessary).");
                file.Locked = true;
            }
            InitiallyLockedBytes = LockedBytes;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    // Only the treatment target may be unlocked. Every other file remains pinned.
    public void ReleaseTarget(string engine, string treatment)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PreparedPageLock));
        if (treatment == "none") return;
        var name = engine == "sqlite" ? "data.sqlite" : treatment switch
        {
            "payload" or "payload-half" => "f0.bin",
            "hashes" => "f1.bin",
            "offsets" => "f2.bin",
            _ => throw new ArgumentException("Unknown cache treatment: " + treatment)
        };
        if (name is null) return;
        var file = _files.Single(f => f.Name == name);
        if (!file.Locked) throw new InvalidOperationException("File already unlocked: " + name);
        if (munlock(file.Address, file.Length) != 0)
            throw new IOException($"munlock({name}) failed: errno={Marshal.GetLastPInvokeError()}");
        file.Locked = false;
        // fadvise(DONTNEED) may retain mapped pages. Unmap the selected file
        // before evicting it; all other file mappings remain locked.
        if (munmap(file.Address, file.Length) != 0)
            throw new IOException($"munmap({name}) failed: errno={Marshal.GetLastPInvokeError()}");
        _files.Remove(file);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Mapping cleanup must proceed even if some unlocks fail.
        foreach (var file in _files.AsEnumerable().Reverse())
        {
            if (file.Locked) munlock(file.Address, file.Length);
            munmap(file.Address, file.Length);
        }
        _files.Clear();
    }
}
