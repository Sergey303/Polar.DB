# Polar.DB

Polar.DB is a .NET library for schema-defined structured data and append-oriented persistent sequences. It is designed for practical storage and retrieval flows where records are represented as Polar values and written to binary streams.

The current main-branch library provides low-level and high-level access patterns. You can work directly in an object-like style (`object[]` values aligned with `PTypeRecord` fields), or use `RecordAccessor` for named field access over the same record layout.

The repository also includes index/search samples around the current public APIs (`USequence`, `SVectorIndex`, `UVectorIndex`, `UVecIndex`, `UIndex`) and tests that protect documented behavior.

## Supported Frameworks

- `netstandard2.0`
- `netstandard2.1`
- `netstandard3.1`
- `net5.0`
- `net6.0`
- `net7.0`
- `net8.0`
- `net9.0`
- `net10.0`

## Quick Start

Both examples below use the same schema and produce the same logical result (`id=1, name=Alice, age=30`).

| Object-like quick start | RecordAccessor-like quick start |
|---|---|
| ```csharp
using Polar.DB;

var personType = new PTypeRecord(
    new NamedType("id", new PType(PTypeEnumeration.integer)),
    new NamedType("name", new PType(PTypeEnumeration.sstring)),
    new NamedType("age", new PType(PTypeEnumeration.integer)));

using var stream = new MemoryStream();
var sequence = new UniversalSequenceBase(personType, stream);
sequence.Clear();
sequence.AppendElement(new object[] { 1, "Alice", 30 });
sequence.Flush();

var person = (object[])sequence.GetElement(8L);
Console.WriteLine($"{person[0]} {person[1]} {person[2]}");
``` | ```csharp
using Polar.DB;

var personType = new PTypeRecord(
    new NamedType("id", new PType(PTypeEnumeration.integer)),
    new NamedType("name", new PType(PTypeEnumeration.sstring)),
    new NamedType("age", new PType(PTypeEnumeration.integer)));

var accessor = new RecordAccessor(personType);
using var stream = new MemoryStream();
var sequence = new UniversalSequenceBase(personType, stream);
sequence.Clear();
sequence.AppendElement(accessor.CreateRecord(1, "Alice", 30));
sequence.Flush();

var person = (object[])sequence.GetElement(8L);
Console.WriteLine($"{accessor.Get<int>(person, "id")} {accessor.Get<string>(person, "name")} {accessor.Get<int>(person, "age")}");
``` |

## Canonical Scenarios

- Defining schemas with `PType`, `PTypeRecord`, `PTypeSequence`, and `NamedType`.
- Writing records/data through append-oriented flows (`AppendElement`, `Flush`, `Load`, `Build` where applicable).
- Reading records/data through sequence scans (`ElementValues`, `GetElement`, `GetByKey`, index lookups).
- Named access through `RecordAccessor` for field-by-name get/set and shape validation.
- Using object-like (`object[]`) record values as the lower-level baseline representation.
- Building and using indexes with currently supported APIs (`SVectorIndex`, `UVectorIndex`, `UVecIndex`, `UIndex`) in `USequence` scenarios.

## Storage Model Constraints

- The logical end of valid data is the primary boundary, not incidental `Stream.Position` cursor movement.
- `AppendOffset` is treated as the logical end-of-valid-data marker for append operations.
- Physical stream length is not automatically the authoritative logical end in all cases (especially during recovery/normalization paths).
- Recovery/refresh normalize state from readable data and logical boundaries before continuing writes.
- Data must be stabilized (written and normalized) before indexes/state are treated as finalized.

## Overwrite Safety

- Do not assume arbitrary variable-size in-place overwrite is safe by default.
- Prefer documented append-oriented and canonical scenarios.
- Only rely on overwrite behaviors that are explicitly demonstrated by current tests/samples.
- A narrow supported overwrite case exists for fixed-size records written at known valid offsets.

## Build and Test

```bash
dotnet build Polar.DB.sln
dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj
```

## Naming

- Canonical public name: `Polar.DB`.
- Legacy `Polar.DB` naming may appear only in preserved historical/external references.

