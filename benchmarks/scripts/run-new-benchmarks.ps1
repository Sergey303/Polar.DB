$ErrorActionPreference = "Stop"

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
  Write-Host "Running $project"
  dotnet run -c Release --project $project
}
