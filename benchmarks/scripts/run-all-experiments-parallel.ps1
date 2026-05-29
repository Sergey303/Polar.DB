param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [int]$MaxParallel = 2,
    [string]$EnvironmentClass = "local",
    [string[]]$Include = @(),
    [string[]]$Exclude = @(),
    [switch]$SkipClean,
    [switch]$SkipCleanAfterEachExperiment,
    [switch]$KeepFailedArtifacts,
    [switch]$ContinueOnFailure
)

$ErrorActionPreference = "Stop"

function Assert-RepoRoot {
    param([string]$Root)

    if (-not (Test-Path -LiteralPath (Join-Path $Root "benchmarks\experiments"))) {
        throw "benchmarks\experiments not found under RepoRoot: $Root"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Root "benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj"))) {
        throw "Polar.DB.Bench.Exec project not found under RepoRoot: $Root"
    }
}

function Test-NameMatch {
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

function Test-NameExcluded {
    param(
        [string]$Name,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Name -like $pattern) {
            return $true
        }
    }

    return $false
}

function Invoke-ExperimentCleanup {
    param(
        [string]$CleanupScript,
        [string]$RepoRoot,
        [string]$ExperimentName
    )

    Write-Host "==> Cleanup temporary files for finished experiment: $ExperimentName"

    & powershell -NoProfile -ExecutionPolicy Bypass `
        -File $CleanupScript `
        -RepoRoot $RepoRoot `
        -ExperimentName $ExperimentName `
        -Quiet

    if ($LASTEXITCODE -ne 0) {
        throw "Cleanup failed for '$ExperimentName' with exit code $LASTEXITCODE"
    }
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
Assert-RepoRoot -Root $repo

$experimentsRoot = Join-Path $repo "benchmarks\experiments"
$execProject = Join-Path $repo "benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj"
$cleanupScript = Join-Path $repo "benchmarks\scripts\clean-experiment-temporary-files.ps1"
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$launcherLogRoot = Join-Path $repo "benchmarks\run-logs\$runStamp"

if ($MaxParallel -lt 1) {
    throw "MaxParallel must be >= 1."
}

if (-not (Test-Path -LiteralPath $cleanupScript)) {
    throw "Cleanup script not found: $cleanupScript"
}

if (-not $SkipClean) {
    Write-Host "==> Cleaning benchmark temporary files before creating new artifacts"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $cleanupScript -RepoRoot $repo
    if ($LASTEXITCODE -ne 0) {
        throw "Cleanup failed with exit code $LASTEXITCODE"
    }
}

New-Item -ItemType Directory -Force -Path $launcherLogRoot | Out-Null

$experiments = Get-ChildItem -LiteralPath $experimentsRoot -Directory -Force |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "experiment.json") } |
    Where-Object { Test-NameMatch -Name $_.Name -Patterns $Include } |
    Where-Object { -not (Test-NameExcluded -Name $_.Name -Patterns $Exclude) } |
    Sort-Object Name

if ($experiments.Count -eq 0) {
    throw "No experiments selected."
}

Write-Host "==> Selected experiments: $($experiments.Count)"
Write-Host "==> MaxParallel: $MaxParallel"
Write-Host "==> Logs: $launcherLogRoot"
Write-Host "==> Clean after each experiment: $(-not $SkipCleanAfterEachExperiment)"

$queue = [System.Collections.Queue]::new()
foreach ($experiment in $experiments) {
    $queue.Enqueue($experiment)
}

$running = New-Object System.Collections.Generic.List[object]
$results = New-Object System.Collections.Generic.List[object]

while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $MaxParallel) {
        $experiment = $queue.Dequeue()
        $name = $experiment.Name
        $stdout = Join-Path $launcherLogRoot "$name.stdout.log"
        $stderr = Join-Path $launcherLogRoot "$name.stderr.log"

        Write-Host "==> Start: $name"

        $job = Start-Job -Name $name -ScriptBlock {
            param($RepoRoot, $ExecProject, $ExperimentPath, $EnvironmentClass, $StdoutPath, $StderrPath)

            Set-Location $RepoRoot

            & dotnet run --project $ExecProject -- --exp $ExperimentPath --env $EnvironmentClass `
                > $StdoutPath `
                2> $StderrPath

            return $LASTEXITCODE
        } -ArgumentList $repo, $execProject, $experiment.FullName, $EnvironmentClass, $stdout, $stderr

        $running.Add([pscustomobject]@{
            Name = $name
            Job = $job
            Stdout = $stdout
            Stderr = $stderr
        }) | Out-Null
    }

    Start-Sleep -Seconds 1

    $finished = @($running | Where-Object { $_.Job.State -in @("Completed", "Failed", "Stopped") })
    foreach ($item in $finished) {
        $exitCode = $null
        try {
            $jobOutput = Receive-Job -Job $item.Job -ErrorAction SilentlyContinue
            if ($jobOutput.Count -gt 0) {
                $exitCode = [int]$jobOutput[-1]
            }
        }
        catch {
            $exitCode = -1
        }

        if ($null -eq $exitCode) {
            $exitCode = -1
        }

        Remove-Job -Job $item.Job -Force -ErrorAction SilentlyContinue

        $cleanupExitCode = 0
        if (-not $SkipCleanAfterEachExperiment -and ($exitCode -eq 0 -or -not $KeepFailedArtifacts)) {
            try {
                Invoke-ExperimentCleanup -CleanupScript $cleanupScript -RepoRoot $repo -ExperimentName $item.Name
            }
            catch {
                $cleanupExitCode = 1
                Write-Host "==> CLEANUP FAILED: $($item.Name)"
                Write-Host "    $($_.Exception.Message)"
            }
        }
        elseif ($KeepFailedArtifacts -and $exitCode -ne 0) {
            Write-Host "==> Keep failed experiment artifacts: $($item.Name)"
        }

        $effectiveExitCode = if ($exitCode -ne 0) { $exitCode } elseif ($cleanupExitCode -ne 0) { $cleanupExitCode } else { 0 }

        $results.Add([pscustomobject]@{
            Experiment = $item.Name
            ExitCode = $exitCode
            CleanupExitCode = $cleanupExitCode
            EffectiveExitCode = $effectiveExitCode
            Stdout = $item.Stdout
            Stderr = $item.Stderr
        }) | Out-Null

        if ($effectiveExitCode -eq 0) {
            Write-Host "==> OK: $($item.Name)"
        }
        else {
            Write-Host "==> FAILED: $($item.Name), exit code $exitCode, cleanup exit code $cleanupExitCode"
            Write-Host "    stderr: $($item.Stderr)"

            if (-not $ContinueOnFailure) {
                while ($queue.Count -gt 0) {
                    [void]$queue.Dequeue()
                }
            }
        }

        [void]$running.Remove($item)
    }
}

$summaryPath = Join-Path $launcherLogRoot "summary.csv"
$results | Export-Csv -Path $summaryPath -NoTypeInformation -Encoding UTF8

$failed = @($results | Where-Object { $_.EffectiveExitCode -ne 0 })
Write-Host ""
Write-Host "==> Summary: $summaryPath"
Write-Host "==> Completed: $($results.Count)"
Write-Host "==> Failed:    $($failed.Count)"

if ($failed.Count -gt 0) {
    $failed | Format-Table -AutoSize
    exit 1
}

exit 0
