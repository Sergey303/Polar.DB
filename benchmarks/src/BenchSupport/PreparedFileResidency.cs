using System.Runtime.InteropServices;

namespace PolarDbBenchmarks;

// mincore() queries page-cache residency without reading the mapped file.
// Linux only; run this between stages, never inside a timed query loop.
internal static class PreparedFileResidency
{
    internal sealed record Snapshot(long Pages, long ResidentPages, double Fraction);
    private const int ProtRead = 1;
    private const int MapPrivate = 2;

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    private static extern nint Mmap(nint address, nuint length, int prot,
        int flags, int fd, long offset);

    [DllImport("libc", EntryPoint = "mincore", SetLastError = true)]
    private static extern int Mincore(nint address, nuint length, byte[] residency);

    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    private static extern int Munmap(nint address, nuint length);

    public static Snapshot? Capture(string directory, IEnumerable<string> relativePaths)
    {
        if (!OperatingSystem.IsLinux()) return null;
        long pages = 0, resident = 0;
        var pageSize = Environment.SystemPageSize;
        foreach (var relative in relativePaths)
        {
            var path = Path.Combine(directory, relative);
            using var stream = File.OpenRead(path);
            if (stream.Length == 0) continue;
            var size = checked((nuint)stream.Length);
            var count = checked((int)((stream.Length + pageSize - 1) / pageSize));
            var mapping = Mmap(0, size, ProtRead, MapPrivate,
                checked((int)stream.SafeFileHandle.DangerousGetHandle()), 0);
            if (mapping == -1)
                throw new IOException($"mmap failed on {path}: errno={Marshal.GetLastPInvokeError()}");
            try
            {
                var vector = new byte[count];
                if (Mincore(mapping, size, vector) != 0)
                    throw new IOException($"mincore failed on {path}: errno={Marshal.GetLastPInvokeError()}");
                pages += count;
                foreach (var value in vector)
                    resident += value & 1;
            }
            finally
            {
                if (Munmap(mapping, size) != 0)
                    throw new IOException($"munmap failed on {path}: errno={Marshal.GetLastPInvokeError()}");
            }
        }
        return new Snapshot(pages, resident, pages == 0 ? 0 : (double)resident / pages);
    }
}
