using Common;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    public static void Main()
    {
        Section.Run("Typed DbSet basics", RunBasics);
        Section.Run("Typed DbSet reopen", RunReopen);
        Section.Run("Typed primary key map", RunPrimaryKeyMap);
        Section.Run("Typed lookup API", RunLookupApi);
        Section.Run("Typed batch API", RunBatchApi);
        Section.Run("Typed constructor config", RunConstructorConfig);
        Section.Run("Typed key type guard", RunKeyTypeGuard);
        Section.Run("Typed diagnostics", RunDiagnostics);
        Section.Run("Typed external key map", RunExternalKeyMap);
        Section.Run("Typed append mutation safety", RunAppendMutationSafety);
        Section.Run("Typed concurrent access", RunConcurrentAccess);
        Section.Run("Typed scheme build guard", RunSchemeBuildGuard);
        Section.Run("Typed scheme compatibility guard", RunSchemeCompatibilityGuard);
        Section.Run("Typed schema file safety", RunSchemaFileSafety);
        Section.Run("Typed DbSet lifecycle guard", RunDbSetLifecycleGuard);
    }
}
