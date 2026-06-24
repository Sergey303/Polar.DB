using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

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
            PersonSchema.IsEmpty,
            PersonSchema.PrimaryKey,
            PersonSchema.HashKey);

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
