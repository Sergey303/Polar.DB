using Common;

namespace GetStarted.StructuresAndSerialization;

internal static class Program
{
    public static void Main()
    {
        Section.Run("Person record serialization", PersonRecordSerialization.Run);
    }
}