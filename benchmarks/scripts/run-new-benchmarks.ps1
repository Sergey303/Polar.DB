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

foreach ($project in $projects) {
  $projectName = Split-Path (Split-Path $project -Parent) -Leaf
  $experimentId = Get-ExperimentId $projectName
  $work = Join-Path "benchmarks\work" $experimentId

  if (Test-Path $work) {
    Write-Host "Cleaning $work before $project"
    Remove-Item $work -Recurse -Force
  }

  try {
    Write-Host "Running $project"
    dotnet run -c Release --project $project
  }
  finally {
    if (Test-Path $work) {
      Write-Host "Cleaning $work after $project"
      Remove-Item $work -Recurse -Force
    }
  }
}
