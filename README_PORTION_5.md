# Portion 5: clean GetStarted.SequencesAndStorage scenario infrastructure

This portion removes the old scenario runner files from `samples/GetStarted.SequencesAndStorage` and restores a clean SDK project file without `Compile Remove` exclusions.

It keeps the simple no-args mode:

- `PersonDatabaseObjectArray.Run()`
- `PersonDatabaseRecordAccessor.Run()`
- `SchedulingOptimizationExample.Run()`

The script deletes these obsolete files if they exist:

- `ISampleScenario.cs`
- `ScenarioCatalog.cs`
- `Scenarios/`
