#Requires -Version 7

<#
.SYNOPSIS
    Captures what each Showroom test prints, for the output panels on the Examples pages.

.DESCRIPTION
    Runs the Showroom lanes with a TRX logger and harvests each test's standard output - which is
    where xunit collects ITestOutputHelper writes, and therefore where a timeline's run log ends up.
    The result is written to showroom-output.json, which is committed on purpose:

      * capturing needs a Docker daemon and a restorable package feed,
      * building the site should need neither.

    Volatile detail inside the captured output (timestamps, ids, absolute paths, ports, box padding) is
    normalised, so re-capturing an unchanged Showroom produces no diff in what a panel *shows*. If that
    churns, the normalisation is incomplete - fix it here rather than committing noise.

    Durations are the deliberate exception: the exact measured figure is kept, together with the machine
    that produced it, so a page can state both the number and what it actually means.

    A skipped test is captured too. Its reason is the honest documentation of a prerequisite.

.PARAMETER ShowroomPath
    The Showroom working copy or submodule to run.

.PARAMETER Lane
    Restrict to one lane, e.g. TestFramework.Showroom.Basic. Defaults to every lane.

.PARAMETER Fresh
    Write only what this run produced instead of merging into an existing file. Used by CI, where the
    pipeline run is the source of truth.

.PARAMETER OutputFile
    Where this capture is written. It is a working file under run-data/, not the published one:
    Merge-ShowroomOutput.ps1 turns one or more captures into showroom-output.json.

.EXAMPLE
    ./Capture-ShowroomOutput.ps1 -ShowroomPath ../TestFramework-Showroom -Lane TestFramework.Showroom.Basic
    ./Merge-ShowroomOutput.ps1 -Path run-data/captures
#>

[CmdletBinding()]
param(
    [string] $ShowroomPath = (Join-Path $PSScriptRoot 'Modules' 'TestFramework-Showroom'),
    [string[]] $Lane,
    [string] $OutputFile = (Join-Path $PSScriptRoot 'run-data' 'captures' 'showroom-capture.json'),
    [switch] $Fresh
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $ShowroomPath)) { throw "No Showroom at $ShowroomPath." }

$showroom = (Resolve-Path $ShowroomPath).Path
$results = Join-Path $PSScriptRoot 'staging' 'trx'
Remove-Item $results -Recurse -Force -ErrorAction Ignore
New-Item $results -ItemType Directory -Force | Out-Null

function Get-DurationMilliseconds([string] $Duration) {
    # The exact figure, deliberately. It is the one field in this file that is expected to differ
    # between captures, which is why the captured output itself is normalised: a diff in stdout then
    # means the example changed, while a diff here only means time passed differently.
    [timespan] $span = [timespan]::Zero
    if (-not [timespan]::TryParse($Duration, [ref] $span)) { return $null }
    return [math]::Round($span.TotalMilliseconds, 1)
}

function Get-CaptureEnvironment {
    <#
        Timings are only meaningful with the machine attached. A GitHub runner answers "does this run in
        a pipeline", not "how fast is this on your laptop", and a page that shows a number without saying
        which of those it measured is inviting the wrong conclusion.
    #>
    $isCi = $env:GITHUB_ACTIONS -eq 'true'

    return [ordered]@{
        kind = if ($isCi) { 'GitHub Actions' } else { 'local machine' }
        os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription.Trim()
        image = if ($env:ImageOS) { $env:ImageOS } else { $null }
        processors = [Environment]::ProcessorCount
    }
}

function Convert-ToStableText([string] $Text) {
    if (-not $Text) { return $Text }

    # Anything that changes between two identical runs, replaced by what it means.
    $stable = $Text
    $stable = $stable -replace '\d{4}-\d{2}-\d{2}([T ]\s*)\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?', '<timestamp>'
    $stable = $stable -replace '\d{2}:\d{2}:\d{2}\.\d+', '<time>'
    $stable = $stable -replace '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', '<guid>'
    # The framework also mints ids without hyphens - artifact names, run ids - and they are the
    # single biggest source of capture churn. The debug view wraps long values across box lines, so
    # fragments as short as eight characters show up; requiring a digit keeps ordinary words safe.
    $stable = [regex]::Replace($stable, '\b(?=[0-9a-f]*[0-9])[0-9a-f]{8,}\b', '<id>', 'IgnoreCase')
    $stable = $stable -replace [regex]::Escape($showroom), '<showroom>'
    $stable = $stable -replace '[A-Za-z]:\\[^\s"'']*', '<path>'

    # The debug view wraps long values across box lines, so a path can be split and only its first
    # slice matches the pattern above. Redact the account name on its own as well - a fragment that
    # happens to start mid-path must not be the thing that publishes it.
    if ($env:USERNAME) { $stable = $stable -replace [regex]::Escape($env:USERNAME), '<user>' }
    if ($env:USERPROFILE) {
        $leaf = Split-Path $env:USERPROFILE -Leaf
        if ($leaf) { $stable = $stable -replace [regex]::Escape($leaf), '<user>' }
    }
    $stable = $stable -replace '\b\d+(\.\d+)?\s?ms\b', '<duration>'
    $stable = $stable -replace 'localhost:\d{4,5}', 'localhost:<port>'
    $stable = $stable -replace '127\.0\.0\.1:\d{4,5}', '127.0.0.1:<port>'
    return (Convert-ToAlignedBoxArt $stable).TrimEnd()
}

function Convert-ToAlignedBoxArt([string] $Text) {
    <#
        The timeline debug view draws boxes, and nests a narrower one inside a wider one. Once a
        variable-length value inside a box has been normalised, the padding in front of the closing
        border still records how long the original value was - so two identical runs would differ by
        a space or two, and showroom-output.json would churn on every capture.

        Each line that closes with a border is therefore re-padded to the width of the box it is in,
        which is the width of the last corner line seen. Corner lines carry no variable content, so
        that width is stable by construction, and the art keeps the alignment it was drawn with.
    #>
    $corner = [char[]] @([char]0x256D, [char]0x256E, [char]0x256F, [char]0x2570)
    $border = [char]0x2502
    $width = 0

    $aligned = foreach ($line in $Text.Replace("`r`n", "`n").Split("`n")) {
        if ($line.IndexOfAny($corner) -ge 0) {
            $width = $line.Length
            $line
            continue
        }

        if ($width -gt 0 -and $line.EndsWith($border) -and $line -match '^(?<content>.*\S)[ ]*.$') {
            $content = $Matches['content']
            $padding = $width - $content.Length - 1
            if ($padding -lt 1) { $line } else { $content + (' ' * $padding) + $border }
            continue
        }

        $line
    }

    return $aligned -join "`n"
}

$lanes = Get-ChildItem $showroom -Directory -Filter 'TestFramework.Showroom.*'
if ($Lane) { $lanes = $lanes | Where-Object { $Lane -contains $_.Name } }
if (-not $lanes) { throw 'No matching lane.' }

foreach ($current in $lanes) {
    $project = Join-Path $current.FullName ($current.Name + '.csproj')
    if (-not (Test-Path $project)) { continue }

    Write-Host "==> Running $($current.Name)" -ForegroundColor Cyan

    # A failing test must not abort the capture: a red chapter is still a chapter, and its output is
    # exactly what a reader hitting the same failure needs to compare against.
    dotnet test $project -c Release --logger "trx;LogFileName=$($current.Name).trx" --results-directory $results
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "$($current.Name) reported failures; capturing its output anyway."
    }
}

$tests = [ordered]@{}

foreach ($file in Get-ChildItem $results -Filter '*.trx') {
    [xml] $trx = Get-Content $file.FullName -Raw
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
    $namespaceManager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

    $definitions = @{}
    foreach ($definition in $trx.SelectNodes('//t:TestDefinitions/t:UnitTest', $namespaceManager)) {
        $method = $definition.SelectSingleNode('t:TestMethod', $namespaceManager)
        $definitions[$definition.id] = '{0}.{1}' -f $method.className, $method.name
    }

    foreach ($result in $trx.SelectNodes('//t:Results/t:UnitTestResult', $namespaceManager)) {
        $name = $definitions[$result.testId]
        if (-not $name) { $name = $result.testName }

        $standardOutput = $result.SelectSingleNode('t:Output/t:StdOut', $namespaceManager)
        $errorMessage = $result.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $namespaceManager)

        # xunit reports a skip as NotExecuted, carrying the skip reason as the message.
        $skipped = $result.outcome -eq 'NotExecuted'

        $tests[$name] = [ordered]@{
            outcome = if ($skipped) { 'Skipped' } else { $result.outcome }
            durationMs = if ($result.duration) { Get-DurationMilliseconds $result.duration } else { $null }
            skipReason = if ($skipped -and $errorMessage) { Convert-ToStableText $errorMessage.InnerText } else { $null }
            stdout = if ($standardOutput) { Convert-ToStableText $standardOutput.InnerText } else { $null }
        }
    }
}

if ($tests.Count -eq 0) { throw 'No test results were parsed out of the TRX files.' }

$commit = (git -C $showroom rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { $commit = $null }

# Locally, capturing one lane must not discard the others, so the existing file is merged into.
# In CI the run is the source of truth and -Fresh skips that: what this run produced is all there is,
# including skips. Merging there would let a stale pass outlive the environment that produced it.
$existing = if (-not $Fresh -and (Test-Path $OutputFile)) { Get-Content $OutputFile -Raw | ConvertFrom-Json -AsHashtable } else { $null }
$merged = [ordered]@{}
if ($existing -and $existing.ContainsKey('tests')) {
    foreach ($key in ($existing.tests.Keys | Sort-Object)) { $merged[$key] = $existing.tests[$key] }
}

foreach ($key in $tests.Keys) { $merged[$key] = $tests[$key] }

$sorted = [ordered]@{}
foreach ($key in ($merged.Keys | Sort-Object)) { $sorted[$key] = $merged[$key] }

New-Item (Split-Path $OutputFile -Parent) -ItemType Directory -Force | Out-Null

# One full record, timings included. Merge-ShowroomOutput.ps1 decides what of this is publishable and
# what stays in the run - see ShowroomOutput.Common.ps1 for why they are not the same thing.
[ordered]@{
    capturedFromCommit = $commit
    capturedAt = (Get-Date -Format 'yyyy-MM-dd')
    capturedIn = Get-CaptureEnvironment
    tests = $sorted
} | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding utf8NoBOM

Write-Host "==> Captured $($tests.Count) tests into $OutputFile" -ForegroundColor Cyan
