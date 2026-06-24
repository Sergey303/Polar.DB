$ErrorActionPreference = "Stop"

$repo = "D:\projects\Polar.DB"
Set-Location $repo

$out = Join-Path $repo "samples-inventory-portion7.txt"
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }

function Add-Line([string]$text = "") {
    Add-Content -LiteralPath $out -Value $text -Encoding UTF8
}

function Rel([string]$path) {
    return $path.Substring($repo.Length + 1)
}

Add-Line "Polar.DB samples inventory"
Add-Line "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Line "Repo: $repo"
Add-Line ""

$projects = Get-ChildItem ".\samples" -Recurse -File -Filter *.csproj | Sort-Object FullName
Add-Line "== Sample projects =="
foreach ($project in $projects) {
    Add-Line (Rel $project.FullName)
}
Add-Line ""

Add-Line "== Program.cs files =="
$programs = Get-ChildItem ".\samples" -Recurse -File -Filter Program.cs | Sort-Object FullName
foreach ($program in $programs) {
    $rel = Rel $program.FullName
    $text = Get-Content -LiteralPath $program.FullName -Raw
    $usesArgs = $text -match '\bargs\b' -or $text -match 'Environment\.GetCommandLineArgs'
    $hasTopLevel = -not ($text -match 'static\s+void\s+Main|static\s+Task\s+Main|public\s+static\s+void\s+Main|public\s+static\s+Task\s+Main')
    Add-Line "$rel | usesArgs=$usesArgs | topLevel=$hasTopLevel"
}
Add-Line ""

Add-Line "== Scenario infrastructure references =="
$scenarioHits = Select-String -Path ".\samples\**\*.cs" -Pattern "ISampleScenario|ScenarioCatalog|\blist\b|\ball\b" -ErrorAction SilentlyContinue
if ($scenarioHits) {
    foreach ($hit in $scenarioHits) {
        Add-Line ("{0}:{1}: {2}" -f (Rel $hit.Path), $hit.LineNumber, $hit.Line.Trim())
    }
} else {
    Add-Line "No scenario infrastructure references found."
}
Add-Line ""

Add-Line "== Static Run classes =="
$csFiles = Get-ChildItem ".\samples" -Recurse -File -Filter *.cs | Sort-Object FullName
foreach ($file in $csFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -match 'static\s+class\s+([A-Za-z0-9_]+)' -and $text -match 'public\s+static\s+void\s+Run\s*\(') {
        Add-Line (Rel $file.FullName)
    }
}
Add-Line ""

Add-Line "== Non-static candidate classes with Run or scenario smell =="
foreach ($file in $csFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if (($text -match 'class\s+([A-Za-z0-9_]+)' -and $text -notmatch 'static\s+class') -and
        ($text -match '\bRun\s*\(' -or $text -match 'ISampleScenario|ScenarioCatalog')) {
        Add-Line (Rel $file.FullName)
    }
}
Add-Line ""

Add-Line "== Build check =="
foreach ($project in $projects) {
    $rel = Rel $project.FullName
    Add-Line "BUILD $rel"
    dotnet build $project.FullName -v:q -clp:ErrorsOnly | Out-String | ForEach-Object { Add-Line $_.TrimEnd() }
    Add-Line "BUILD_EXIT=$LASTEXITCODE"
    Add-Line ""
}

Write-Host "Inventory written to $out"
Get-Content -LiteralPath $out | Select-Object -First 200
