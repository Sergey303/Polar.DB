using Common;

namespace GetStarted.IndexesAndSearch;

internal static class Program
{
    public static void Main()
    {
        Section.Run("Primary key lookup", PrimaryKeyLookup.Run);
        Section.Run("Age index search", AgeIndexSearch.Run);
        Section.Run("Tag and prefix search", TagAndPrefixSearch.Run);
        Section.Run("Text token search", TextTokenSearch.Run);
        Section.Run("External key search", ExternalKeySearch.Run);
        Section.Run("Hash function comparison", HashFunctionComparison.Run);
        Section.Run("Index scale smoke", IndexScaleSmoke.Run);
    }
}
