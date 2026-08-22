#Requires -Version 7

<#
.SYNOPSIS
    Combines the per-lane capture files one pipeline run produced into showroom-output.json.

.DESCRIPTION
    The lanes are captured in parallel, each writing its own file, because a lane that needs Docker
    should not hold up one that does not. This joins them.

    It writes two files. showroom-output.json holds what an example printed and whether it passed, and is
    committed. The measurements file holds the timings and the machine that took them, lives under
    run-data/, and is never committed - a duration is true of one run on one machine and of nothing else.

    The committed file is replaced rather than merged into: the pipeline run is the source of truth, so a
    chapter CI could only skip is published as skipped. That is a deliberate choice -
    the alternative keeps a passing panel alive after the environment that produced it is gone, and a
    reader cannot tell the difference.

    Every capture carries the Showroom commit it came from. They must agree: two lanes captured from
    different commits would produce a file that describes no single state of the code.

.PARAMETER Path
    The per-lane capture files, or directories containing them.

.PARAMETER OutputFile
    Where the combined file is written.

.EXAMPLE
    ./Merge-ShowroomOutput.ps1 -Path ./captures -OutputFile ./showroom-output.json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $Path,
    [string] $OutputFile = (Join-Path $PSScriptRoot 'showroom-output.json'),
    [string] $MeasurementsFile = (Join-Path $PSScriptRoot 'run-data' 'showroom-measurements.json')
)

. (Join-Path $PSScriptRoot 'ShowroomOutput.Common.ps1')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$files = foreach ($candidate in $Path) {
    if (Test-Path -PathType Container $candidate) {
        Get-ChildItem $candidate -Filter '*.json' -Recurse
    }
    elseif (Test-Path $candidate) {
        Get-Item $candidate
    }
    else {
        throw "No capture at $candidate."
    }
}

$files = @($files)
if ($files.Count -eq 0) { throw 'No capture files found; nothing to merge.' }

$tests = [ordered]@{}
$commits = [System.Collections.Generic.HashSet[string]]::new()
$environments = [System.Collections.Generic.HashSet[string]]::new()
$processorCounts = [System.Collections.Generic.HashSet[string]]::new()
$capturedAt = $null
$capturedIn = $null

foreach ($file in $files) {
    $capture = Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable

    if ($capture.capturedFromCommit) { [void] $commits.Add($capture.capturedFromCommit) }
    if (-not $capturedAt -and $capture.capturedAt) { $capturedAt = $capture.capturedAt }

    if ($capture.capturedIn) {
        # Compare the machine *class*, not the instance. A matrix runs each lane on its own runner, so
        # the instance differs by design; what must agree is the kind of machine, because that is what
        # a published timing claims. Core count is reported rather than enforced: it varies within a
        # runner class and a reader is told which machine the figures came from anyway.
        $class = '{0}|{1}|{2}' -f $capture.capturedIn.kind, $capture.capturedIn.image, $capture.capturedIn.os
        [void] $environments.Add($class)
        [void] $processorCounts.Add([string] $capture.capturedIn.processors)
        if (-not $capturedIn) { $capturedIn = $capture.capturedIn }
    }

    if (-not $capture.ContainsKey('tests')) { continue }

    foreach ($name in $capture.tests.Keys) {
        if ($tests.Contains($name)) {
            # Two lanes claiming the same test means the capture was scoped wrongly; silently keeping
            # one of them would publish a panel nobody can account for.
            throw "$name appears in more than one capture file."
        }

        $tests[$name] = $capture.tests[$name]
    }

    Write-Host ("    {0}: {1} tests" -f $file.Name, $capture.tests.Count)
}

if ($commits.Count -gt 1) {
    throw ('Captures come from different Showroom commits ({0}); they cannot describe one state.' -f ($commits -join ', '))
}

if ($environments.Count -gt 1) {
    throw ('Captures come from different kinds of machine ({0}), so their durations cannot be published as one set.' -f ($environments -join ' / '))
}

if ($processorCounts.Count -gt 1) {
    Write-Warning ('Lanes ran on runners with different core counts ({0}); timings are comparable only loosely.' -f ($processorCounts -join ', '))
}

if ($tests.Count -eq 0) { throw 'The capture files hold no test results.' }

$commit = if ($commits.Count -eq 1) { @($commits)[0] } else { $null }

Write-ShowroomOutput `
    -Tests $tests `
    -Commit $commit `
    -Environment $capturedIn `
    -OutputFile $OutputFile `
    -MeasurementsFile $MeasurementsFile

Write-Host ("==> Merged {0} lane captures, {1} tests, into {2}" -f $files.Count, $tests.Count, $OutputFile) -ForegroundColor Cyan
