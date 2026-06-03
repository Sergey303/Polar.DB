using System.Runtime.InteropServices;

namespace PolarDbBenchmarks;

internal static class BenchmarkSystemMemory
{
    public static long AvailableBytes()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return WindowsAvailableBytes();
            if (OperatingSystem.IsLinux()) return LinuxAvailableBytes();
        }
        catch
        {
            return FallbackBytes();
        }

        return FallbackBytes();
    }

    private static long WindowsAvailableBytes()
    {
        var status = MemoryStatusEx.Create();
        return GlobalMemoryStatusEx(ref status) ? checked((long)status.AvailPhys) : FallbackBytes();
    }

    private static long LinuxAvailableBytes()
    {
        const string prefix = "MemAvailable:";
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var raw = line[prefix.Length..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return long.Parse(raw) * 1024L;
        }

        return FallbackBytes();
    }

    private static long FallbackBytes() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public static MemoryStatusEx Create()
        {
            var result = new MemoryStatusEx();
            result.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
            return result;
        }
    }
}
