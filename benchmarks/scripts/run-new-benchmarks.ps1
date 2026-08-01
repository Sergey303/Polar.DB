$ErrorActionPreference = "Stop"

function Get-ExperimentId([string] $projectName) {
  switch ($projectName) {
    "PkIntLookup" { return "pk-int-lookup" }
    "PkLongLookup" { return "pk-long-lookup" }
    "PkGuidLookup" { return "pk-guid-lookup" }
    "PkStringLookup" { return "pk-string-lookup" }
    "ExternalIntLookup" { return "external-int-lookup" }
    "ExternalLongLookup" { return "external-long-lookup" }
    "ExternalGuidLookup" { return "external-guid-lookup" }
    "ExternalStringLookup" { return "external-string-lookup" }
    "ExternalFamousIntLookup" { return "external-famous-int-lookup" }
    "ExternalFamousLongLookup" { return "external-famous-long-lookup" }
    "ExternalFamousGuidLookup" { return "external-famous-guid-lookup" }
    "ExternalFamousStringLookup" { return "external-famous-string-lookup" }
    "BuildPrimaryIntOnly" { return "build-primary-int-only" }
    "ReopenOnly" { return "reopen-only" }
    "AppendOnly" { return "append-only" }
    "DeleteOnly" { return "delete-only" }
    default { return $projectName }
  }
}

$projects = @(
  "benchmarks\src\PkIntLookup\PkIntLookup.csproj",
  "benchmarks\src\PkLongLookup\PkLongLookup.csproj",
  "benchmarks\src\PkGuidLookup\PkGuidLookup.csproj",
  "benchmarks\src\PkStringLookup\PkStringLookup.csproj",
  "benchmarks\src\ExternalIntLookup\ExternalIntLookup.csproj",
  "benchmarks\src\ExternalLongLookup\ExternalLongLookup.csproj",
  "benchmarks\src\ExternalGuidLookup\ExternalGuidLookup.csproj",
  "benchmarks\src\ExternalStringLookup\ExternalStringLookup.csproj",
  "benchmarks\src\ExternalFamousIntLookup\ExternalFamousIntLookup.csproj",
  "benchmarks\src\ExternalFamousLongLookup\ExternalFamousLongLookup.csproj",
  "benchmarks\src\ExternalFamousGuidLookup\ExternalFamousGuidLookup.csproj",
  "benchmarks\src\ExternalFamousStringLookup\ExternalFamousStringLookup.csproj",
  "benchmarks\src\BuildPrimaryIntOnly\BuildPrimaryIntOnly.csproj",
  "benchmarks\src\ReopenOnly\ReopenOnly.csproj",
  "benchmarks\src\AppendOnly\AppendOnly.csproj",
  "benchmarks\src\DeleteOnly\DeleteOnly.csproj"
)

$previousRunId = $env:POLAR_BENCH_RUN_ID
$seriesId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssfffZ")
$env:POLAR_BENCH_RUN_ID = $seriesId

try {
  Write-Host "Benchmark series: $seriesId"
  Write-Host "Building shared benchmark library"
  dotnet build -c Release "benchmarks\src\BenchSupport\BenchSupport.csproj"

  foreach ($project in $projects) {
    $projectName = Split-Path (Split-Path $project -Parent) -Leaf
    $experimentId = Get-ExperimentId $projectName
    Write-Host "Running $experimentId from $project"
    dotnet run -c Release --project $project
  }
}
finally {
  if ($null -eq $previousRunId) {
    Remove-Item Env:POLAR_BENCH_RUN_ID -ErrorAction SilentlyContinue
  }
  else {
    $env:POLAR_BENCH_RUN_ID = $previousRunId
  }
}
