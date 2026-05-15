param(
    [string]$Configuration = "Release",
    [string]$Experiment = "benchmarks/experiments/string-like-prefix-lookup/experiment.json",
    [string]$Output = "benchmarks/results/raw/string-like-prefix-lookup.latest.json"
)

$ErrorActionPreference = "Stop"

Write-Host "Build benchmarks..."
dotnet build benchmarks -c $Configuration

Write-Host "Run string-like-prefix-lookup..."
dotnet run --project benchmarks/src/Polar.DB.Bench.Exec `
  -c $Configuration -- `
  --experiment $Experiment `
  --output $Output
