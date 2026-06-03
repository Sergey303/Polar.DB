param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [int]$MaxParallel = 2,
    [string]$EnvironmentClass = "local",
    [string[]]$Include = @(),
    [string[]]$Exclude = @(),
    [switch]$SkipClean,
    [switch]$SkipCleanAfterEachExperiment,
    [switch]$KeepFailedArtifacts,
    [switch]$ContinueOnFailure,
    [switch]$SkipPreBuild,
    [switch]$CleanupDryRun,
    [int]$ProgressIntervalSeconds = 15,
    [switch]$QuietCleanup,
    [switch]$ShowLiveLogTail
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    $stamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$stamp] $Message"
}

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
        [string]$ExperimentName,
        [bool]$DryRun,
        [bool]$Quiet
    )

    if ($DryRun) {
        Write-Step "CLEANUP DRY-RUN: $ExperimentName"
    }
    else {
        Write-Step "CLEANUP: $ExperimentName"
    }

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $CleanupScript,
        "-RepoRoot", $RepoRoot,
        "-ExperimentName", $ExperimentName
    )

    if ($Quiet) {
        $args += @("-Quiet")
    }

    if ($DryRun) {
        $args += @("-DryRun:`$true")
    }
    else {
        $args += @("-Apply")
    }

    & powershell @args

    if ($LASTEXITCODE -ne 0) {
        throw "Cleanup failed for '$ExperimentName' with exit code $LASTEXITCODE"
    }
}

function Invoke-DotNetBuildOnce {
    param(
        [string]$RepoRoot,
        [string]$LogRoot
    )

    $projects = @(
        "benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj",
        "benchmarks\src\Polar.DB.Bench.Analysis\Polar.DB.Bench.Analysis.csproj",
        "benchmarks\src\Polar.DB.Bench.Charts\Polar.DB.Bench.Charts.csproj",
        "benchmarks\src\Polar.DB.Bench.Exec.PolarDbCurrent\Polar.DB.Bench.Exec.PolarDbCurrent.csproj",
        "benchmarks\src\Polar.DB.Bench.Exec.PolarDb210\Polar.DB.Bench.Exec.PolarDb210.csproj",
        "benchmarks\src\Polar.DB.Bench.Exec.PolarDb211\Polar.DB.Bench.Exec.PolarDb211.csproj"
    )

    foreach ($relative in $projects) {
        $project = Join-Path $RepoRoot $relative
        if (-not (Test-Path -LiteralPath $project)) {
            continue
        }

        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        $stdout = Join-Path $LogRoot ("build.$name.stdout.log")
        $stderr = Join-Path $LogRoot ("build.$name.stderr.log")

        Write-Step "BUILD: $relative"
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList @("build", $project, "--nologo", "-v:minimal", "/m:1", "/nr:false") `
            -WorkingDirectory $RepoRoot `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr `
            -Wait `
            -PassThru

        if ($process.ExitCode -ne 0) {
            Write-Step "BUILD FAILED: $relative"
            Write-Step "stdout: $stdout"
            Write-Step "stderr: $stderr"
            throw "Build failed for '$relative' with exit code $($process.ExitCode)."
        }
    }
}

function Format-Duration {
    param([TimeSpan]$Duration)

    if ($Duration.TotalHours -ge 1) {
        return "{0:00}:{1:00}:{2:00}" -f [int]$Duration.TotalHours, $Duration.Minutes, $Duration.Seconds
    }

    return "{0:00}:{1:00}" -f $Duration.Minutes, $Duration.Seconds
}

function Get-LastNonEmptyLine {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        $lines = Get-Content -LiteralPath $Path -Tail 20 -ErrorAction Stop
        for ($i = $lines.Count - 1; $i -ge 0; $i--) {
            $line = [string]$lines[$i]
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                return $line.Trim()
            }
        }
    }
    catch {
        return $null
    }

    return $null
}

function Write-RunningStatus {
    param(
        [System.Collections.Generic.List[object]]$Running,
        [switch]$ShowTail
    )

    if ($Running.Count -eq 0) {
        return
    }

    Write-Step ("RUNNING: {0} active experiment(s)" -f $Running.Count)
    foreach ($item in $Running | Sort-Object Name) {
        $elapsed = Format-Duration -Duration (New-TimeSpan -Start $item.StartedAt -End (Get-Date))
        Write-Step ("  active: {0}, elapsed={1}" -f $item.Name, $elapsed)
        Write-Step ("    stdout: {0}" -f $item.Stdout)
        Write-Step ("    stderr: {0}" -f $item.Stderr)

        if ($ShowTail) {
            $lastStdout = Get-LastNonEmptyLine -Path $item.Stdout
            $lastStderr = Get-LastNonEmptyLine -Path $item.Stderr
            if ($lastStdout) { Write-Step ("    last stdout: {0}" -f $lastStdout) }
            if ($lastStderr) { Write-Step ("    last stderr: {0}" -f $lastStderr) }
        }
    }
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
Assert-RepoRoot -Root $repo

$experimentsRoot = Join-Path $repo "benchmarks\experiments"
$execProject = Join-Path $repo "benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj"
$cleanupScript = Join-Path $repo "benchmarks\scripts\clean-experiment-temporary-files.ps1"
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$launcherLogRoot = Join-Path $repo "benchmarks\run-logs\$runStamp"
$summaryTextPath = Join-Path $launcherLogRoot "summary.txt"

if ($MaxParallel -lt 1) {
    throw "MaxParallel must be >= 1."
}

if ($ProgressIntervalSeconds -lt 1) {
    throw "ProgressIntervalSeconds must be >= 1."
}

if (-not (Test-Path -LiteralPath $cleanupScript)) {
    throw "Cleanup script not found: $cleanupScript"
}

New-Item -ItemType Directory -Force -Path $launcherLogRoot | Out-Null

# Make dotnet output stable and readable in redirected logs.
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_UI_LANGUAGE = "en"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"

Write-Step "Polar.DB benchmark parallel runner"
Write-Step "RepoRoot: $repo"
Write-Step "Logs:     $launcherLogRoot"
Write-Step "Parallel: $MaxParallel"
Write-Step "Cleanup before run:       $(-not $SkipClean)"
Write-Step "Cleanup after experiment: $(-not $SkipCleanAfterEachExperiment)"
Write-Step "Cleanup dry-run:          $CleanupDryRun"
Write-Step "Keep failed artifacts:    $KeepFailedArtifacts"
Write-Step "Progress interval:        $ProgressIntervalSeconds sec"
Write-Step "Show live log tail:       $ShowLiveLogTail"
Write-Step "Quiet cleanup:            $QuietCleanup"

if (-not $SkipClean) {
    if ($CleanupDryRun) {
        Write-Step "PRE-CLEAN DRY-RUN: benchmark temporary files"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $cleanupScript -RepoRoot $repo -DryRun:$true
    }
    else {
        Write-Step "PRE-CLEAN: benchmark temporary files"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $cleanupScript -RepoRoot $repo -Apply
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Cleanup failed with exit code $LASTEXITCODE"
    }
}

if (-not $SkipPreBuild) {
    Invoke-DotNetBuildOnce -RepoRoot $repo -LogRoot $launcherLogRoot
}
else {
    Write-Step "BUILD: skipped by -SkipPreBuild"
}

$experiments = Get-ChildItem -LiteralPath $experimentsRoot -Directory -Force |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "experiment.json") } |
    Where-Object { Test-NameMatch -Name $_.Name -Patterns $Include } |
    Where-Object { -not (Test-NameExcluded -Name $_.Name -Patterns $Exclude) } |
    Sort-Object Name

if ($experiments.Count -eq 0) {
    throw "No experiments selected."
}

Write-Step "Selected experiments: $($experiments.Count)"
foreach ($experiment in $experiments) {
    Write-Step "  queued: $($experiment.Name)"
}

$queue = [System.Collections.Queue]::new()
foreach ($experiment in $experiments) {
    $queue.Enqueue($experiment)
}

$running = New-Object System.Collections.Generic.List[object]
$results = New-Object System.Collections.Generic.List[object]
$lastProgressAt = Get-Date

while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $MaxParallel) {
        $experiment = $queue.Dequeue()
        $name = $experiment.Name
        $stdout = Join-Path $launcherLogRoot "$name.stdout.log"
        $stderr = Join-Path $launcherLogRoot "$name.stderr.log"

        Write-Step "START: $name"
        Write-Step "  stdout: $stdout"
        Write-Step "  stderr: $stderr"

        $job = Start-Job -Name $name -ScriptBlock {
            param($RepoRoot, $ExecProject, $ExperimentPath, $EnvironmentClass, $StdoutPath, $StderrPath)

            Set-Location $RepoRoot
            $env:DOTNET_NOLOGO = "1"
            $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
            $env:DOTNET_CLI_UI_LANGUAGE = "en"
            $env:MSBUILDDISABLENODEREUSE = "1"
            $env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"

            $process = Start-Process -FilePath "dotnet" `
                -ArgumentList @("run", "--no-build", "--project", $ExecProject, "--", "--exp", $ExperimentPath, "--env", $EnvironmentClass) `
                -WorkingDirectory $RepoRoot `
                -RedirectStandardOutput $StdoutPath `
                -RedirectStandardError $StderrPath `
                -Wait `
                -PassThru

            return $process.ExitCode
        } -ArgumentList $repo, $execProject, $experiment.FullName, $EnvironmentClass, $stdout, $stderr

        $running.Add([pscustomobject]@{
            Name = $name
            Job = $job
            Stdout = $stdout
            Stderr = $stderr
            StartedAt = Get-Date
        }) | Out-Null
    }

    Start-Sleep -Seconds 1

    if ((New-TimeSpan -Start $lastProgressAt -End (Get-Date)).TotalSeconds -ge $ProgressIntervalSeconds) {
        Write-RunningStatus -Running $running -ShowTail:$ShowLiveLogTail
        $lastProgressAt = Get-Date
    }

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

        $duration = New-TimeSpan -Start $item.StartedAt -End (Get-Date)
        Write-Step ("FINISH: {0}, exit={1}, elapsed={2}" -f $item.Name, $exitCode, (Format-Duration -Duration $duration))

        $cleanupExitCode = 0
        if (-not $SkipCleanAfterEachExperiment -and ($exitCode -eq 0 -or -not $KeepFailedArtifacts)) {
            try {
                Invoke-ExperimentCleanup -CleanupScript $cleanupScript -RepoRoot $repo -ExperimentName $item.Name -DryRun:$CleanupDryRun -Quiet:$QuietCleanup
            }
            catch {
                $cleanupExitCode = 1
                Write-Step "CLEANUP FAILED: $($item.Name)"
                Write-Step "  $($_.Exception.Message)"
            }
        }
        elseif ($KeepFailedArtifacts -and $exitCode -ne 0) {
            Write-Step "KEEP: failed experiment artifacts preserved for $($item.Name)"
        }

        if ($exitCode -ne 0) {
            $effectiveExitCode = $exitCode
        }
        elseif ($cleanupExitCode -ne 0) {
            $effectiveExitCode = $cleanupExitCode
        }
        else {
            $effectiveExitCode = 0
        }

        $results.Add([pscustomobject]@{
            Experiment = $item.Name
            ExitCode = $exitCode
            CleanupExitCode = $cleanupExitCode
            EffectiveExitCode = $effectiveExitCode
            Elapsed = $duration.ToString()
            Stdout = $item.Stdout
            Stderr = $item.Stderr
        }) | Out-Null

        if ($effectiveExitCode -eq 0) {
            Write-Step "OK: $($item.Name)"
        }
        else {
            Write-Step "FAILED: $($item.Name), exit=$exitCode, cleanup=$cleanupExitCode"
            Write-Step "  stdout: $($item.Stdout)"
            Write-Step "  stderr: $($item.Stderr)"

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
$ok = @($results | Where-Object { $_.EffectiveExitCode -eq 0 })

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add("Polar.DB benchmark run summary")
$summaryLines.Add("Run stamp: $runStamp")
$summaryLines.Add("RepoRoot:  $repo")
$summaryLines.Add("Logs:      $launcherLogRoot")
$summaryLines.Add("Completed: $($results.Count)")
$summaryLines.Add("OK:        $($ok.Count)")
$summaryLines.Add("Failed:    $($failed.Count)")
$summaryLines.Add("")
foreach ($result in $results) {
    $summaryLines.Add(("{0} | exit={1} cleanup={2} effective={3} elapsed={4}" -f `
        $result.Experiment, $result.ExitCode, $result.CleanupExitCode, $result.EffectiveExitCode, $result.Elapsed))
    $summaryLines.Add("  stdout: $($result.Stdout)")
    $summaryLines.Add("  stderr: $($result.Stderr)")
}
[IO.File]::WriteAllLines($summaryTextPath, $summaryLines, [Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Step "Summary CSV:  $summaryPath"
Write-Step "Summary text: $summaryTextPath"
Write-Step "Completed:    $($results.Count)"
Write-Step "OK:           $($ok.Count)"
Write-Step "Failed:       $($failed.Count)"

if ($failed.Count -gt 0) {
    $failed | Format-Table -AutoSize
    exit 1
}

exit 0
