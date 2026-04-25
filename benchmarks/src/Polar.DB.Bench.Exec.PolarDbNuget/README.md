# Polar.DB.Bench.Exec.PolarDbNuget

Reflective runner for benchmarking old NuGet versions of `Polar.DB` without compile-time references.

The project intentionally has **no** `PackageReference` / `ProjectReference` to `Polar.DB`. This lets one runner execute workloads against `Polar.DB` 2.1.0, 2.1.1, or another NuGet package version whose public namespaces differ, for example when one version needs `using Polar.Universal` and another must not use it.

## Recommended placement

```text
benchmarks/src/Polar.DB.Bench.Exec.PolarDbNuget/
```

Add it to the benchmark solution:

```bash
dotnet sln benchmarks/Polar.DB.Bench.sln add benchmarks/src/Polar.DB.Bench.Exec.PolarDbNuget/Polar.DB.Bench.Exec.PolarDbNuget.csproj
```

## Probe mode

Use probe mode first. It loads the requested NuGet DLL in an isolated `AssemblyLoadContext`, inspects candidate Polar.DB types, and writes a JSON report.

```bash
dotnet run --project benchmarks/src/Polar.DB.Bench.Exec.PolarDbNuget -- \
  --mode probe \
  --engine-key polar-db-2.1.1 \
  --package-version 2.1.1 \
  --tfm netstandard2.0 \
  --output benchmarks/results/raw/probe.polar-db-2.1.1.json
```

Or pass a DLL directly:

```bash
dotnet run --project benchmarks/src/Polar.DB.Bench.Exec.PolarDbNuget -- \
  --mode probe \
  --engine-key polar-db-2.1.0 \
  --polar-dll C:\Users\you\.nuget\packages\polar.db\2.1.0\lib\netstandard2.0\Polar.DB.dll \
  --output benchmarks/results/raw/probe.polar-db-2.1.0.json
```

## Run mode

```bash
dotnet run --project benchmarks/src/Polar.DB.Bench.Exec.PolarDbNuget -- \
  --mode run \
  --engine-key polar-db-2.1.1 \
  --package-version 2.1.1 \
  --tfm netstandard2.0 \
  --experiment benchmarks/experiments/polar-db-nuget-smoke.experiment.json \
  --work-dir benchmarks/.work/polar-db-2.1.1/smoke \
  --output benchmarks/results/raw/polar-db-2.1.1.smoke.raw.json
```

## Important note

The runner contains a conservative reflective API shape for the common `Load -> Build -> Refresh -> Lookup` workflow. Because older Polar.DB APIs may differ in constructor signatures and method names, the first execution should be done in `probe` mode. If the exact API surface differs, adjust only `Workloads/ReflectivePolarDbApiShape.cs`.

Reflection is used only during binding. Hot operations are called through compiled delegates in `Reflection/FastMethodInvoker.cs`; this avoids measuring `MethodInfo.Invoke` on every operation.
