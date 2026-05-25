param(
    [string] $RepoRoot,

    [string] $ExperimentsRoot,

    [string] $RunnerProject,

    [int] $MaxParallel = [Math]::Max(1, [Environment]::ProcessorCount - 1),

    [string] $Configuration = "Release",

    [string] $Framework = "",

    [string] $ResultsRoot,

    [string[]] $Include = @(),

    [string[]] $Exclude = @(),

    [switch] $NoBuild,

    [switch] $WhatIf
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    return [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine((Get-Location).Path, $Path)
    )
}

function Get-DefaultRepoRoot {
    if ($PSScriptRoot) {
        return [System.IO.Path]::GetFullPath(
            [System.IO.Path]::Combine($PSScriptRoot, "..", "..")
        )
    }

    return (Get-Location).Path
}

function Find-FirstExistingDirectory([string[]] $Candidates) {
    foreach ($candidate in $Candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Find-RunnerProject([string] $Root) {
    $candidates = @(
        (Join-Path $Root "benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj"),
        (Join-Path $Root "benchmarks\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj"),
        (Join-Path $Root "benchmarks\src\BenchExec\BenchExec.csproj")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $found = Get-ChildItem `
        -LiteralPath (Join-Path $Root "benchmarks") `
        -Recurse `
        -Filter "*.csproj" `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -match "Bench\.Exec|BenchExec"
        } |
        Select-Object -First 1

    if ($found) {
        return $found.FullName
    }

    return $null
}

function Get-SafeName([string] $Path) {
    $relative = $Path
    try {
        $relative = [System.IO.Path]::GetRelativePath($ExperimentsRoot, $Path)
    }
    catch {
        $relative = [System.IO.Path]::GetFileName($Path)
    }

    return ($relative -replace '[\\/:*?"<>|]', '_')
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Get-DefaultRepoRoot
}
else {
    $RepoRoot = Resolve-FullPath $RepoRoot
}

if (-not (Test-Path -LiteralPath $RepoRoot -PathType Container)) {
    throw "RepoRoot не найден: $RepoRoot"
}

if ([string]::IsNullOrWhiteSpace($ExperimentsRoot)) {
    $ExperimentsRoot = Find-FirstExistingDirectory @(
        (Join-Path $RepoRoot "benchmarks\work"),
        (Join-Path $RepoRoot "benchmarks\experiments"),
        (Join-Path $RepoRoot "benchmarks\scenarios")
    )
}
else {
    $ExperimentsRoot = Resolve-FullPath $ExperimentsRoot
}

if (-not $ExperimentsRoot -or -not (Test-Path -LiteralPath $ExperimentsRoot -PathType Container)) {
    throw "ExperimentsRoot не найден. Передай -ExperimentsRoot явно."
}

if ([string]::IsNullOrWhiteSpace($RunnerProject)) {
    $RunnerProject = Find-RunnerProject $RepoRoot
}
else {
    $RunnerProject = Resolve-FullPath $RunnerProject
}

if (-not $RunnerProject -or -not (Test-Path -LiteralPath $RunnerProject -PathType Leaf)) {
    throw "RunnerProject не найден. Передай -RunnerProject явно."
}

if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
    $ResultsRoot = Join-Path $RepoRoot "benchmarks\results\parallel-runs\$timestamp"
}
else {
    $ResultsRoot = Resolve-FullPath $ResultsRoot
}

$LogsRoot = Join-Path $ResultsRoot "logs"
New-Item -ItemType Directory -Force -Path $ResultsRoot, $LogsRoot | Out-Null

$experiments = Get-ChildItem `
    -LiteralPath $ExperimentsRoot `
    -Recurse `
    -File `
    -Filter "experiment.json" |
    Sort-Object FullName

if ($Include.Count -gt 0) {
    $experiments = $experiments | Where-Object {
        $path = $_.FullName
        $Include | Where-Object { $path -like "*$_*" }
    }
}

if ($Exclude.Count -gt 0) {
    $experiments = $experiments | Where-Object {
        $path = $_.FullName
        -not ($Exclude | Where-Object { $path -like "*$_*" })
    }
}

$experiments = @($experiments)

if ($experiments.Count -eq 0) {
    throw "Не найдено ни одного experiment.json в $ExperimentsRoot"
}

Write-Host "RepoRoot:        $RepoRoot"
Write-Host "ExperimentsRoot: $ExperimentsRoot"
Write-Host "RunnerProject:   $RunnerProject"
Write-Host "ResultsRoot:     $ResultsRoot"
Write-Host "MaxParallel:     $MaxParallel"
Write-Host "Experiments:     $($experiments.Count)"
Write-Host ""

if (-not $NoBuild) {
    Write-Host "Building runner..."
    $buildArgs = @(
        "build",
        "`"$RunnerProject`"",
        "-c",
        $Configuration
    )

    if (-not [string]::IsNullOrWhiteSpace($Framework)) {
        $buildArgs += @("-f", $Framework)
    }

    $buildCmd = "dotnet " + ($buildArgs -join " ")
    Write-Host $buildCmd

    cmd /c $buildCmd

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build завершился с кодом $LASTEXITCODE"
    }

    Write-Host ""
}

if ($WhatIf) {
    Write-Host "WhatIf: будут запущены:"
    foreach ($experiment in $experiments) {
        Write-Host "  $($experiment.FullName)"
    }

    exit 0
}

$jobs = New-Object System.Collections.Generic.List[object]
$results = New-Object System.Collections.Generic.List[object]
$queue = New-Object System.Collections.Queue

foreach ($experiment in $experiments) {
    $queue.Enqueue($experiment.FullName)
}

$scriptBlock = {
    param(
        [string] $ExperimentPath,
        [string] $RunnerProject,
        [string] $Configuration,
        [string] $Framework,
        [string] $ResultsRoot,
        [string] $LogsRoot,
        [string] $SafeName
    )

    $startedAt = Get-Date
    $stdoutPath = Join-Path $LogsRoot "$SafeName.stdout.log"
    $stderrPath = Join-Path $LogsRoot "$SafeName.stderr.log"

    $args = @(
        "run",
        "--project",
        "`"$RunnerProject`"",
        "-c",
        $Configuration,
        "--no-build"
    )

    if (-not [string]::IsNullOrWhiteSpace($Framework)) {
        $args += @("-f", $Framework)
    }

    $args += @(
        "--",
        "--experiment",
        "`"$ExperimentPath`"",
        "--results-root",
        "`"$ResultsRoot`""
    )

    $command = "dotnet " + ($args -join " ")

    $exitCode = 0
    try {
        cmd /c "$command 1> `"$stdoutPath`" 2> `"$stderrPath`""
        $exitCode = $LASTEXITCODE
    }
    catch {
        $exitCode = -1
        $_ | Out-File -FilePath $stderrPath -Encoding UTF8 -Append
    }

    $finishedAt = Get-Date

    [pscustomobject]@{
        Experiment = $ExperimentPath
        ExitCode   = $exitCode
        StartedAt  = $startedAt
        FinishedAt = $finishedAt
        Duration   = $finishedAt - $startedAt
        StdOut     = $stdoutPath
        StdErr     = $stderrPath
        Command    = $command
    }
}

function Start-ExperimentJob([string] $ExperimentPath) {
    $safeName = Get-SafeName $ExperimentPath

    Start-Job `
        -ScriptBlock $scriptBlock `
        -ArgumentList @(
            $ExperimentPath,
            $RunnerProject,
            $Configuration,
            $Framework,
            $ResultsRoot,
            $LogsRoot,
            $safeName
        )
}

while ($queue.Count -gt 0 -or $jobs.Count -gt 0) {
    while ($queue.Count -gt 0 -and $jobs.Count -lt $MaxParallel) {
        $experimentPath = [string] $queue.Dequeue()
        Write-Host "START $experimentPath"
        $job = Start-ExperimentJob $experimentPath
        $jobs.Add($job)
    }

    Start-Sleep -Seconds 1

    $completed = @($jobs | Where-Object {
        $_.State -in @("Completed", "Failed", "Stopped")
    })

    foreach ($job in $completed) {
        $jobResult = Receive-Job -Job $job -ErrorAction SilentlyContinue

        if ($jobResult) {
            $results.Add($jobResult)

            if ($jobResult.ExitCode -eq 0) {
                Write-Host "OK    $($jobResult.Experiment)"
            }
            else {
                Write-Host "FAIL  $($jobResult.Experiment) exit=$($jobResult.ExitCode)"
                Write-Host "      stderr: $($jobResult.StdErr)"
            }
        }
        else {
            Write-Host "FAIL  job did not return result: $($job.Id)"
        }

        Remove-Job -Job $job -Force

        [void] $jobs.Remove($job)
    }
}

$summaryPath = Join-Path $ResultsRoot "parallel-run-summary.json"

$results |
    Sort-Object Experiment |
    ConvertTo-Json -Depth 8 |
    Out-File -FilePath $summaryPath -Encoding UTF8

$failed = @($results | Where-Object { $_.ExitCode -ne 0 })

Write-Host ""
Write-Host "Summary: $summaryPath"
Write-Host "Logs:    $LogsRoot"
Write-Host "Total:   $($results.Count)"
Write-Host "Failed:  $($failed.Count)"

if ($failed.Count -gt 0) {
    exit 1
}

exit 0