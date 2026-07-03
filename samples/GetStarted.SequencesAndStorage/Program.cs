using Common;

namespace GetStarted.SequencesAndStorage;

internal static class Program
{
    public static void Main()
    {
        Section.Run("Person database with object arrays", PersonDatabaseObjectArray.Run);
        Section.Run("Person database with RecordAccessor", PersonDatabaseRecordAccessor.Run);
        Section.Run("Scheduling optimization", SchedulingOptimization.Run);
    }
}
