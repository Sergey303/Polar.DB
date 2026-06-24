using Common;
using Polar.DB;
using System.Text;

namespace GetStarted.StructuresAndSerialization;

internal static class PersonRecordSerialization
{
    public static void Run()
    {
        Console.WriteLine("Schema:");
        Console.WriteLine("  record(Id: integer, Name: sstring, Age: integer)");
        Console.WriteLine();

        object original = PersonSchema.Create(1, "Анна Иванова", 30);
        Console.WriteLine("Original record:");
        Console.WriteLine("  " + PersonSchema.Format(original));
        Console.WriteLine();

        object restored = RoundTrip(original);
        Check.Equal(PersonSchema.GetId(original), PersonSchema.GetId(restored),
            "Restored id must match");
        Check.Equal(PersonSchema.GetName(original), PersonSchema.GetName(restored),
            "Restored name must match");
        Check.Equal(PersonSchema.GetAge(original), PersonSchema.GetAge(restored),
            "Restored age must match");

        Console.WriteLine("Restored record after ByteFlow round-trip:");
        Console.WriteLine("  " + PersonSchema.Format(restored));
    }

    private static object RoundTrip(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            ByteFlow.Serialize(writer, value, PersonSchema.Type);
            writer.Flush();
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return ByteFlow.Deserialize(reader, PersonSchema.Type);
    }
}