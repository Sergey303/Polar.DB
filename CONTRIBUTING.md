# Contributing to Polar.DB

Thank you for contributing to Polar.DB.

This repository contains a .NET library for schema-defined structured data, append-oriented sequence persistence, and index-based lookup scenarios. Contributions are welcome, but correctness matters more than surface-level churn.

## Before you start

Please read:

- [README.md](README.md)
- [docs/REPOSITORY_STATE.md](docs/REPOSITORY_STATE.md)
- [SECURITY.md](SECURITY.md)

For changes that affect storage semantics, sequence recovery, indexing, or public API behavior, review the current repository invariants before making code changes.

## Development environment

The repository pins its SDK version through `global.json`.

Recommended setup:

- .NET SDK matching `global.json`
- Git
- Visual Studio 2022 / Rider / VS Code, or another editor with good .NET support

Build and test:

```bash
dotnet restore
dotnet build Polar.DB.sln
dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj
```

## Repository layout

- `src/Polar.DB/` — library source
- `tests/Polar.DB.Tests/` — regression and behavior tests
- `samples/` — sample applications
- `docs/` — repository and project documentation

## What we accept

We welcome:

- bug fixes;
- tests for repaired behavior;
- documentation improvements;
- sample improvements;
- API cleanup that reduces ambiguity without breaking documented behavior;
- carefully justified performance or storage-model improvements.

## What makes a good contribution

A good contribution is:

- focused on one logical change;
- tested;
- documented when public behavior changes;
- explicit about compatibility and risk;
- respectful of historical design constraints and current invariants.

## Pull request expectations

Each pull request should:

- explain **what changed**;
- explain **why it changed**;
- explain **how it was tested**;
- state whether behavior is breaking, non-breaking, or behavior-preserving;
- update docs when public behavior, supported usage, or limitations change.

For bug fixes, include a regression test whenever reasonably possible.

## Storage-model and correctness rules

These rules are especially important in this repository:

1. **Do not confuse physical stream position with logical valid-data boundary.**
2. **Do not assume `Stream.Length` is always the correct logical end of data.**
3. **Treat `AppendOffset` as a logical append boundary, not just a mutable cursor.**
4. **Do not assume arbitrary variable-size in-place overwrite is safe.**
5. **Data must be stabilized before indexes/state are treated as finalized.**
6. **Prefer semantic record access over magic indexes where clarity improves.**

Changes that touch recovery, refresh, append logic, overwrite behavior, or index ordering should be made conservatively and accompanied by tests.

## Public API and documentation style

For new public-facing material:

- prefer the canonical name `Polar.DB`;
- keep historical references only where they are genuinely historical;
- write public top-level community files in English;
- preserve historical documentation unless there is a deliberate migration plan;
- avoid overstating maturity or guarantees.

## Commit style

A strict commit format is not required, but clear commit messages are strongly preferred.

Good examples:

- `Fix first-match boundary handling in UKeyIndex`
- `Add regression tests for recovery with garbage tail`
- `Clarify AppendOffset semantics in README`

## Breaking changes

Breaking changes are allowed only when clearly justified.

A breaking PR must:

- mark itself as breaking;
- explain migration impact;
- update docs;
- avoid mixing unrelated refactoring into the same PR.

## Security issues

Do **not** open a public issue for a suspected vulnerability. Follow [SECURITY.md](SECURITY.md).

## Questions

For usage questions, bug reports, and feature requests, see [SUPPORT.md](SUPPORT.md).
