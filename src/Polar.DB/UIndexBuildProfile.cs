namespace Polar.Universal
{
    public sealed class UIndexBuildProfile
    {
        public static readonly UIndexBuildProfile Empty = new UIndexBuildProfile(0, 0, 0, 0, 0, 0, 0);

        public UIndexBuildProfile(
            double scanMs,
            double toArrayMs,
            double sortMs,
            double writeHashKeysMs,
            double writeOffsetsMs,
            double gcMs,
            double totalMs)
        {
            ScanMs = scanMs;
            ToArrayMs = toArrayMs;
            SortMs = sortMs;
            WriteHashKeysMs = writeHashKeysMs;
            WriteOffsetsMs = writeOffsetsMs;
            GcMs = gcMs;
            TotalMs = totalMs;
        }

        public double ScanMs { get; }
        public double ToArrayMs { get; }
        public double SortMs { get; }
        public double WriteHashKeysMs { get; }
        public double WriteOffsetsMs { get; }
        public double GcMs { get; }
        public double TotalMs { get; }
    }
}
