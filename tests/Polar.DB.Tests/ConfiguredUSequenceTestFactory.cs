using Polar.Universal;

namespace Polar.DB.Tests;

internal static class ConfiguredUSequenceTestFactory
{
    public static USequence CreateIntegerSequence(string tempDir, string statePath, bool optimise = false)
    {
        if (tempDir == null) throw new ArgumentNullException(nameof(tempDir));
        if (statePath == null) throw new ArgumentNullException(nameof(statePath));

        Directory.CreateDirectory(tempDir);
        int counter = 0;

        Stream StreamGen()
        {
            string streamPath = Path.Combine(tempDir, $"useq_{counter++:D4}.bin");
            return new FileStream(streamPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var sequence = new USequence(
            new PType(PTypeEnumeration.integer),
            statePath,
            StreamGen,
            _ => false,
            optimise: optimise);
        sequence.SetPrimaryKey<int>(value => (int)value);
        return sequence;
    }
}
