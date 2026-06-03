$ErrorActionPreference = "Stop"

$projects = @(
  "benchmarks\src\PkIntLookup\PkIntLookup.csproj",
  "benchmarks\src\PkStringLookup\PkStringLookup.csproj",
  "benchmarks\src\ExternalIntLookup\ExternalIntLookup.csproj",
  "benchmarks\src\ExternalStringLookup\ExternalStringLookup.csproj",
  "benchmarks\src\BuildOnly\BuildOnly.csproj",
  "benchmarks\src\ReopenOnly\ReopenOnly.csproj",
  "benchmarks\src\AppendOnly\AppendOnly.csproj",
  "benchmarks\src\DeleteOnly\DeleteOnly.csproj"
)

foreach ($project in $projects) {
  Write-Host "Running $project"
  dotnet run -c Release --project $project
}
