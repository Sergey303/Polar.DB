$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$exec = Join-Path $root "src/Polar.DB.Bench.Exec/Polar.DB.Bench.Exec.csproj"

$experiments = @(
  "string-like-prefix-lookup-indexed",
  "string-like-prefix-lookup-scan",
  "string-like-contains-scan"
)

foreach ($experiment in $experiments) {
  Write-Host "Running $experiment" -ForegroundColor Cyan
  dotnet run --project $exec -- --exp (Join-Path $root "experiments/$experiment")
}
