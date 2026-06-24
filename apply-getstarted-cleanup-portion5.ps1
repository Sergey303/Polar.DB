$ErrorActionPreference = "Stop"

$repo = "D:\projects\Polar.DB"
Set-Location $repo

$obsoleteFiles = @(
  ".\samples\GetStarted.SequencesAndStorage\ISampleScenario.cs",
  ".\samples\GetStarted.SequencesAndStorage\ScenarioCatalog.cs"
)

foreach ($file in $obsoleteFiles) {
  if (Test-Path -LiteralPath $file) {
    Remove-Item -LiteralPath $file -Force
    Write-Host "Removed $file"
  }
}

$obsoleteDirs = @(
  ".\samples\GetStarted.SequencesAndStorage\Scenarios"
)

foreach ($dir in $obsoleteDirs) {
  if (Test-Path -LiteralPath $dir) {
    Remove-Item -LiteralPath $dir -Recurse -Force
    Write-Host "Removed $dir"
  }
}

$requiredFiles = @(
  ".\samples\GetStarted.SequencesAndStorage\GetStarted.SequencesAndStorage.csproj",
  ".\samples\GetStarted.SequencesAndStorage\Program.cs",
  ".\samples\GetStarted.SequencesAndStorage\PersonSchema.cs",
  ".\samples\GetStarted.SequencesAndStorage\PersonSequence.cs",
  ".\samples\GetStarted.SequencesAndStorage\PersonDatabaseObjectArray.cs",
  ".\samples\GetStarted.SequencesAndStorage\PersonDatabaseRecordAccessor.cs",
  ".\samples\GetStarted.SequencesAndStorage\SchedulingOptimizationExample.cs"
)

foreach ($file in $requiredFiles) {
  if (-not (Test-Path -LiteralPath $file)) {
    throw "Required file not found: $file"
  }
}

Write-Host "GetStarted.SequencesAndStorage cleanup completed."
