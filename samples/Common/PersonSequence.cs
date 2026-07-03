using Polar.Universal;

namespace Common;

public static class PersonSequence
{
    public static USequence Open(string dbPath)
    {
        Directory.CreateDirectory(dbPath);
        int fileNumber = 0;

        Stream StreamGen()
        {
            string path = Path.Combine(dbPath, $"f{fileNumber++}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var sequence = new USequence(
            PersonSchema.Type,
            Path.Combine(dbPath, "state.bin"),
            StreamGen,
            PersonSchema.Deleted,
            record => PersonSchema.GetId(record),
            key => Convert.ToInt32(key));

        sequence.Refresh();
        return sequence;
    }

    public static USequence OpenAndRestore(string dbPath)
    {
        var sequence = Open(dbPath);
        sequence.RestoreDynamic();
        return sequence;
    }
}
