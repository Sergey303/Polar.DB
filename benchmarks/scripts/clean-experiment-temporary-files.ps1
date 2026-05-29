param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string[]]$ExperimentName = @(),
    [switch]$DryRun,
    [switch]$SkipExperiments,
    [switch]$SkipWork,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host $Message
    }
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

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    if ($DryRun) {
        Write-Info "DRY-RUN delete directory: $Path"
        return
    }

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
        [string]$Path
    )

    if ($DryRun) {
        Write-Info "DRY-RUN delete file: $Path"
        return
    }

    Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
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
        Write-Info "Clean experiment directory: $($dir.Name)"

        Get-ChildItem -LiteralPath $dir.FullName -Force | Where-Object {
            $_.Name -ne "experiment.json"
        } | ForEach-Object {
            if ($_.PSIsContainer) {
                Remove-DirectoryRobust -Path $_.FullName
            }
            else {
                Remove-FileRobust -Path $_.FullName
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
        Write-Info "Clean benchmark work directory: $work"
        Remove-DirectoryRobust -Path $work
        return
    }

    Get-ChildItem -LiteralPath $work -Directory -Force | Where-Object {
        Test-ExperimentSelected -Name $_.Name -Patterns $Patterns
    } | ForEach-Object {
        Write-Info "Clean benchmark work directory: $($_.FullName)"
        Remove-DirectoryRobust -Path $_.FullName
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
                Remove-FileRobust -Path $_.FullName
            }
    }
}

function Clear-KnownTemporaryFiles {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    if ($Patterns.Count -eq 0) {
        Clear-KnownTemporaryFilesUnder -Root (Join-Path $Root "benchmarks")
        return
    }

    $experimentDirs = @(Get-SelectedExperimentDirectories -Root $Root -Patterns $Patterns)
    foreach ($dir in $experimentDirs) {
        Clear-KnownTemporaryFilesUnder -Root $dir.FullName
    }

    $work = Join-Path $Root "benchmarks\work"
    if (Test-Path -LiteralPath $work) {
        Get-ChildItem -LiteralPath $work -Directory -Force | Where-Object {
            Test-ExperimentSelected -Name $_.Name -Patterns $Patterns
        } | ForEach-Object {
            Clear-KnownTemporaryFilesUnder -Root $_.FullName
        }
    }
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
Assert-RepoRoot -Root $repo

Write-Info "RepoRoot: $repo"
Write-Info "DryRun:   $DryRun"
if ($ExperimentName.Count -gt 0) {
    Write-Info "Experiments: $($ExperimentName -join ', ')"
}

if (-not $SkipExperiments) {
    Clear-ExperimentFolders -Root $repo -Patterns $ExperimentName
}

if (-not $SkipWork) {
    Clear-BenchmarkWork -Root $repo -Patterns $ExperimentName
}

Clear-KnownTemporaryFiles -Root $repo -Patterns $ExperimentName

Write-Info "Cleanup completed."
