using Polar.DB;

namespace GetStarted.SequencesAndIndexes.Scenarios;

internal sealed class RecoveryAndRefreshScenario : ISampleScenario
{
    public string Id => "recovery-refresh";
    public string Title => "Recovery and Refresh after reopen";
    public string SourcePath => "Scenarios/06-RecoveryAndRefreshScenario.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        var recordType = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));
        var path = SamplePaths.File("recovery-refresh.bin");

        using (var writeStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            var sequence = new UniversalSequenceBase(recordType, writeStream);
            sequence.Clear();
            sequence.AppendElement(new object[] { 1, "Alice" });
            sequence.AppendElement(new object[] { 2, "Bob" });
            sequence.Flush();
            Console.WriteLine($"Before reopen: count={sequence.Count()}, appendOffset={sequence.AppendOffset}");
        }

        using (var reopenedStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            var sequence = new UniversalSequenceBase(recordType, reopenedStream);
            Console.WriteLine($"After reopen: count={sequence.Count()}, appendOffset={sequence.AppendOffset}");

            sequence.Refresh();
            Console.WriteLine($"After refresh: count={sequence.Count()}, appendOffset={sequence.AppendOffset}");

            var appendOffsetBefore = sequence.AppendOffset;
            var appendedAt = sequence.AppendElement(new object[] { 3, "Carol" });
            sequence.Flush();

            Console.WriteLine($"Appended at offset={appendedAt}, previous appendOffset={appendOffsetBefore}");
            Console.WriteLine($"After append: count={sequence.Count()}, appendOffset={sequence.AppendOffset}");
            Console.WriteLine("Current sequence:");
            foreach (var person in sequence.ElementValues().Cast<object[]>())
            {
                Console.WriteLine($"  id={person[0]}, name={person[1]}");
            }
        }
    }
}
