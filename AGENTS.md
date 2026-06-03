# AGENTS.md

Guidelines for automated coding agents working in this repository.

## Scope

These rules apply to the entire repository unless a more specific `AGENTS.md` is added in a subdirectory.

## Primary goals

- Preserve correctness of persistent data structures and recovery behavior.
- Keep changes small, reviewable, and easy to revert.
- Prefer explicit invariants over implicit stream-position or filesystem assumptions.
- Do not hide behavior changes inside formatting-only commits.

## File size rule

- New files must be no longer than 150 lines.
- When adding logic, prefer small focused files instead of large catch-all files.
- Existing files that already exceed 150 lines are allowed to remain as they are.
- Do not rewrite, split, or reformat existing large files only to satisfy this rule.
- If an existing file is already over 150 lines, make the smallest meaningful change there or extract new logic into a small new file when practical.

## Naming rules for new code

Use standard C# naming conventions for new or substantially rewritten code:

- Types, namespaces, public members, properties, methods, and events: `PascalCase`.
- Local variables and parameters: `camelCase`.
- Private instance fields: `_camelCase`.
- Private static fields: `_camelCase` unless a project-local convention clearly requires otherwise.
- Constants: `PascalCase`.
- Async methods should end with `Async` when they return `Task` or `Task<T>`.

Important compatibility rule:

- Existing files may use naming that differs from these rules.
- Do not rename existing symbols only for style.
- Do not perform broad naming cleanups unless the task explicitly asks for them.
- Public API renames require a clear reason and must be called out in the final response.

## Style and formatting

- Use UTF-8 text files.
- Keep line endings consistent with the touched file.
- Prefer simple, direct code over clever abstractions.
- Avoid unrelated formatting changes.
- Avoid changing whitespace across a whole file when only a small code change is needed.
- Keep comments useful: explain invariants and non-obvious behavior, not syntax.

## Tests

- Add or update tests for behavior changes.
- Prefer focused regression tests for boundary cases.
- For persistence changes, include reopen/recovery-oriented tests when practical.
- Do not remove tests unless the behavior they validate is intentionally removed.

## Benchmarks and generated artifacts

- Do not commit benchmark run artifacts, temporary databases, generated reports, or local machine outputs unless explicitly requested.
- Keep benchmark changes reproducible: fixed seeds, clear dataset size, clear engine settings.
- Separate measurement logic from analysis/reporting logic where practical.

## Dependencies and target frameworks

- Do not upgrade SDKs, target frameworks, NuGet packages, or workflow versions unless the task explicitly requires it.
- Preserve multi-targeting unless the task explicitly changes the supported framework set.
- Avoid changes that make older supported targets fail without calling this out.

## Final response expectations

When finishing a repository change, report:

- files changed;
- commit hash, if a commit was created;
- tests run, or state clearly that tests were not run;
- any compatibility or public API impact.
