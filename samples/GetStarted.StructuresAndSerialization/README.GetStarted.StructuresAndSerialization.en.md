# GetStarted.StructuresAndSerialization

This is the first tutorial project in the Polar.DB samples.

Use it to understand the basic building blocks of Polar values before moving to storage or indexes.

## What this project teaches

- how `PType` describes data;
- how records, sequences, and other structured values are represented as objects;
- how `Interpret(...)` helps inspect values;
- how text and binary serialization work;
- how schema-aware helpers such as `RecordAccessor` improve readability and safety.
- how object-like and RecordAccessor-like record access stay logically equivalent for the same schema/data.

## Recommended reading order

Start with the simplest scenario, then move toward richer examples.

A good progression is:
1. basic record/type introduction;
2. sequence or richer structured values;
3. serialization round-trip;
4. schema-aware access by field name.

## What you should understand before leaving this project

After finishing this project, you should be comfortable with these ideas:
- a Polar value always has a schema;
- object-shaped values are convenient but still schema-driven;
- serialization is not a separate afterthought, it is part of the normal data workflow;
- named field access is preferable to fragile magic indexes when record structure matters.

## Typical commands

```bash
dotnet run --project samples/GetStarted.StructuresAndSerialization -- list
dotnet run --project samples/GetStarted.StructuresAndSerialization -- all
dotnet run --project samples/GetStarted.StructuresAndSerialization -- <scenario-id>
```

## Notes

This project should stay introductory.

Do not overload it with sequence recovery logic, index internals, or unrelated experiments. Those belong to later tutorial projects.

