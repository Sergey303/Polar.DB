# Polar.DB test files with XML documentation

This package contains additional storage-model tests for `tests/Polar.DB.Tests`.

All added public and internal helper classes, contract interfaces, records, constructors, methods, and properties include useful XML-doc comments. The comments are written to explain the storage invariant being tested, not just to satisfy documentation warnings.

## Files

- `StorageCorruptionHelpers.cs` — shared helpers for direct stream corruption, header manipulation, stale-tail creation, and temporary file-backed tests.
- `UniversalSequenceBaseRecoveryMatrixTests.cs` — recovery matrix for partial headers, overdeclared counts, underdeclared counts, stale tails, and truncated variable-size records.
- `USequenceRepeatedCycleTests.cs` — repeated reopen/append/recover cycles for fixed-size and variable-size sequences.
- `VariableSizeRewriteContractTests.cs` — safety contract for fixed-size and variable-size in-place rewrite behavior.
- `StorageCrashRecoveryTests.cs` — deterministic crash-point simulations around header update, item write, and partial variable-size serialization.
- `PropertyBasedFuzzTests.cs` — deterministic pseudo-random append/reopen/corruption model test.
- `PerformanceSmokeTests.cs` — visible smoke measurements for append throughput and reopen/recovery cost.
- `StateIndexDataDivergenceContractTests.cs` — abstract contract tests and harness interface for data/state/index divergence.
- `BuildConsistencyContractTests.cs` — abstract contract tests for build idempotence and durable searchability.
- `IndexBoundaryContractTests.cs` — abstract contract tests for duplicate-key and missing-key index boundaries.
- `ConcurrencyLockingTests.cs` — small file locking and restart smoke tests.

## Integration note

The `UniversalSequenceBase` tests are intended to be close to drop-in. The abstract contract tests require a concrete implementation of `IIndexedSequenceContractHarness` for the repository's current indexed sequence type.
