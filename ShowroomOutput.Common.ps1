#Requires -Version 7

<#
    Shared by Capture-ShowroomOutput.ps1 and Merge-ShowroomOutput.ps1.

    A capture produces two different kinds of fact, and they belong in different places:

      * What an example printed, and whether it passed or skipped. That is content. It is committed, so
        that building the site needs neither Docker nor a package feed - a clone can render every panel.

      * How long it took, and on what machine. That is a measurement. It is true of one run on one
        machine and of nothing else, so it stays in the run that took it and is never committed.

    Keeping them apart buys two things. The committed file changes only when an example's behaviour
    changes, so a diff always means something. And a timing appears on the site exactly where it was
    measured - a pipeline build shows pipeline numbers, a local build shows none, because it measured
    none.
#>

function Write-ShowroomOutput {
    [CmdletBinding()]
    param(
        # Ordered map of test name -> @{ outcome; durationMs; skipReason; stdout }
        [Parameter(Mandatory)] $Tests,

        [string] $Commit,

        # Where the capture happened: kind, os, image, processors.
        $Environment,

        # The committed file: content only.
        [Parameter(Mandatory)] [string] $OutputFile,

        # The run-local file: everything, including timings. Lives under run-data/ and is git-ignored.
        [string] $MeasurementsFile
    )

    $names = @($Tests.Keys | Sort-Object)

    $content = [ordered]@{}
    foreach ($name in $names) {
        $test = $Tests[$name]
        $content[$name] = [ordered]@{
            outcome = $test.outcome
            skipReason = $test.skipReason
            stdout = $test.stdout
        }
    }

    # No capturedAt here on purpose: a date would make the file differ tomorrow for no reason.
    [ordered]@{
        capturedFromCommit = $Commit
        tests = $content
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding utf8NoBOM

    if (-not $MeasurementsFile) { return }

    $measurements = [ordered]@{}
    foreach ($name in $names) {
        $measurements[$name] = [ordered]@{ durationMs = $Tests[$name].durationMs }
    }

    New-Item (Split-Path $MeasurementsFile -Parent) -ItemType Directory -Force | Out-Null

    [ordered]@{
        capturedFromCommit = $Commit
        capturedAt = (Get-Date -Format 'yyyy-MM-dd')
        capturedIn = $Environment
        tests = $measurements
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $MeasurementsFile -Encoding utf8NoBOM
}
