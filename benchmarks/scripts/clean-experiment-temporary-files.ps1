param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string[]]$ExperimentName = @(),

    # Safe by default: without -Apply, this script only prints what would be deleted.
    [bool]$DryRun = $true,
    [switch]$Apply,

    [switch]$SkipExperiments,
    [switch]$SkipWork,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

if ($Apply) {
    $DryRun = $false
}

$script:DeletedFiles = 0
$script:DeletedDirs = 0
$script:PlannedFiles = 0
$script:PlannedDirs = 0
$script:Skipped = 0
$script:Errors = 0

function Write-Info {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host $Message
    }
}

function Write-Section {
    param([string]$Message)
    Write-Info ""
    Write-Info "==> $Message"
}

function Assert-RepoRoot {
    param([string]$Root)

    if (-not (Test-Path -LiteralPath $Root)) {
        throw "RepoRoot does not exist: $Root"
    }

    $experiments = Join-Path $Root "benchmarks\experiments"
    if (-not (Test-Path -LiteralPath $experiments)) {
        throw "benchmarks\experiments not found under RepoRoot: $Root"
    }
}

function Test-ExperimentSelected {
    param(
        [string]$Name,
        [string[]]$Patterns
    )

    if ($Patterns.Count -eq 0) {
        return $true
    }

    foreach ($pattern in $Patterns) {
        if ($Name -like $pattern) {
            return $true
        }
    }

    return $false
}

function Format-Bytes {
    param([long]$Bytes)

    if ($Bytes -ge 1GB) { return ("{0:N2} GiB" -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ("{0:N2} MiB" -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ("{0:N2} KiB" -f ($Bytes / 1KB)) }
    return "$Bytes B"
}

function Get-PathSizeBytes {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0L
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        return [long]$item.Length
    }

    $sum = 0L
    Get-ChildItem -LiteralPath $Path -Recurse -Force -File -ErrorAction SilentlyContinue | ForEach-Object {
        $sum += [long]$_.Length
    }

    return $sum
}

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Reason = "temporary directory"
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $bytes = Get-PathSizeBytes -Path $Path

    if ($DryRun) {
        $script:PlannedDirs++
        Write-Info ("DRY-RUN dir  [{0}] {1} ({2})" -f $Reason, $Path, (Format-Bytes $bytes))
        return
    }

    Write-Info ("DELETE  dir  [{0}] {1} ({2})" -f $Reason, $Path, (Format-Bytes $bytes))

    # robocopy /MIR from an empty directory handles very deep nested paths
    # better than Remove-Item in Windows PowerShell.
    $emptyDir = Join-Path $env:TEMP ("polar-db-empty-delete-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $emptyDir | Out-Null

    try {
        robocopy $emptyDir $Path /MIR /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
        if ($LASTEXITCODE -gt 7) {
            throw "robocopy failed for '$Path' with exit code $LASTEXITCODE"
        }

        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
        $script:DeletedDirs++
    }
    finally {
        if (Test-Path -LiteralPath $emptyDir) {
            Remove-Item -LiteralPath $emptyDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Remove-FileRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Reason = "temporary file"
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $bytes = Get-PathSizeBytes -Path $Path

    if ($DryRun) {
        $script:PlannedFiles++
        Write-Info ("DRY-RUN file [{0}] {1} ({2})" -f $Reason, $Path, (Format-Bytes $bytes))
        return
    }

    Write-Info ("DELETE  file [{0}] {1} ({2})" -f $Reason, $Path, (Format-Bytes $bytes))
    Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
    $script:DeletedFiles++
}

function Get-SelectedExperimentDirectories {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    $experimentsRoot = Join-Path $Root "benchmarks\experiments"
    Get-ChildItem -LiteralPath $experimentsRoot -Directory -Force |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "experiment.json") } |
        Where-Object { Test-ExperimentSelected -Name $_.Name -Patterns $Patterns } |
        Sort-Object Name
}

function Clear-ExperimentFolders {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    $experimentDirs = @(Get-SelectedExperimentDirectories -Root $Root -Patterns $Patterns)

    foreach ($dir in $experimentDirs) {
        Write-Section "Experiment folder: $($dir.Name)"

        Get-ChildItem -LiteralPath $dir.FullName -Force | Where-Object {
            $_.Name -ne "experiment.json"
        } | ForEach-Object {
            if ($_.PSIsContainer) {
                Remove-DirectoryRobust -Path $_.FullName -Reason "experiment generated output"
            }
            else {
                Remove-FileRobust -Path $_.FullName -Reason "experiment generated file"
            }
        }
    }
}

function Clear-BenchmarkWork {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    $work = Join-Path $Root "benchmarks\work"
    if (-not (Test-Path -LiteralPath $work)) {
        return
    }

    if ($Patterns.Count -eq 0) {
        Write-Section "Benchmark work root"
        Remove-DirectoryRobust -Path $work -Reason "benchmark work"
        return
    }

    Get-ChildItem -LiteralPath $work -Directory -Force | Where-Object {
        Test-ExperimentSelected -Name $_.Name -Patterns $Patterns
    } | ForEach-Object {
        Write-Section "Benchmark work: $($_.Name)"
        Remove-DirectoryRobust -Path $_.FullName -Reason "benchmark work"
    }
}

function Clear-KnownTemporaryFilesUnder {
    param([string]$Root)

    if (-not (Test-Path -LiteralPath $Root)) {
        return
    }

    $patterns = @(
        "*.db",
        "*.db-wal",
        "*.db-shm",
        "*.sqlite",
        "*.sqlite-wal",
        "*.sqlite-shm",
        "*.state",
        "*.index",
        "*.hkeys.index",
        "*.offsets.index",
        "*.polar.db",
        "*.tmp",
        "*.malformed"
    )

    foreach ($pattern in $patterns) {
        Get-ChildItem -LiteralPath $Root -Recurse -File -Force -Filter $pattern -ErrorAction SilentlyContinue |
            ForEach-Object {
                Remove-FileRobust -Path $_.FullName -Reason "known temporary pattern $pattern"
            }
    }
}

function Clear-KnownTemporaryFiles {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    if ($Patterns.Count -eq 0) {
        Write-Section "Known temporary files under benchmarks"
        Clear-KnownTemporaryFilesUnder -Root (Join-Path $Root "benchmarks")
        return
    }

    $experimentDirs = @(Get-SelectedExperimentDirectories -Root $Root -Patterns $Patterns)
    foreach ($dir in $experimentDirs) {
        Write-Section "Known temporary files in experiment: $($dir.Name)"
        Clear-KnownTemporaryFilesUnder -Root $dir.FullName
    }

    $work = Join-Path $Root "benchmarks\work"
    if (Test-Path -LiteralPath $work) {
        Get-ChildItem -LiteralPath $work -Directory -Force | Where-Object {
            Test-ExperimentSelected -Name $_.Name -Patterns $Patterns
        } | ForEach-Object {
            Write-Section "Known temporary files in work: $($_.Name)"
            Clear-KnownTemporaryFilesUnder -Root $_.FullName
        }
    }
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
Assert-RepoRoot -Root $repo

Write-Host "Polar.DB benchmark cleanup"
Write-Host "RepoRoot: $repo"
Write-Host "Mode:     $(if ($DryRun) { 'DRY-RUN, no files will be deleted' } else { 'APPLY, files will be deleted' })"
if ($ExperimentName.Count -gt 0) {
    Write-Host "Scope:    $($ExperimentName -join ', ')"
}
else {
    Write-Host "Scope:    all experiments"
}

if (-not $SkipExperiments) {
    Clear-ExperimentFolders -Root $repo -Patterns $ExperimentName
}

if (-not $SkipWork) {
    Clear-BenchmarkWork -Root $repo -Patterns $ExperimentName
}

Clear-KnownTemporaryFiles -Root $repo -Patterns $ExperimentName

Write-Host ""
Write-Host "Cleanup summary"
if ($DryRun) {
    Write-Host "Planned dirs:  $script:PlannedDirs"
    Write-Host "Planned files: $script:PlannedFiles"
    Write-Host "Nothing was deleted. Run with -Apply or -DryRun:`$false to delete."
}
else {
    Write-Host "Deleted dirs:  $script:DeletedDirs"
    Write-Host "Deleted files: $script:DeletedFiles"
}
