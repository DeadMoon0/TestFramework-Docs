#Requires -Version 7

<#
.SYNOPSIS
    Builds the TestFramework documentation site.

.DESCRIPTION
    The API reference is reflected out of the *shipped packages*, not out of source: docs.csproj
    names the versions, this script restores them, and docfx reads each assembly, its XML doc file
    and its symbols. Three consequences worth knowing:

      * The site can only document API a consumer is able to install.
      * No repository has to be cloned or built in order to document it.
      * View-source links come from the SourceLink map inside each .pdb, so every link lands in the
        package's own repository. That is why the .pdb is staged next to the .dll - docfx emits
        those links only when it finds one there.

    Symbols are not part of a restore, so they are fetched separately: out of the feed folder when
    -Feed is a directory, otherwise from the nuget.org flat container.

.PARAMETER Feed
    Package source. A directory (the local feed) or a URL. Defaults to nuget.org.

.PARAMETER Serve
    Serve the site and watch for changes instead of exiting after the build.

.EXAMPLE
    ./Build-Docs.ps1 -Feed ../artifacts/nuget-local -Serve
#>

[CmdletBinding()]
param(
    [string] $Feed = 'https://api.nuget.org/v3/index.json',
    [string] $ShowroomPath = (Join-Path $PSScriptRoot 'Modules' 'TestFramework-Showroom'),
    [switch] $AllowMissingNarration,
    [switch] $Serve
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$packages = Join-Path $root 'packages'
$staging = Join-Path $root 'staging'
$stagedApi = Join-Path $staging 'api'
$stagedRefs = Join-Path $staging 'refs'

# The one target framework the reference documents. The packages multi-target net8.0 and net10.0
# with no #if between them, so the two surfaces are identical and net8.0 is the lower bound a
# consumer can be on.
$documentedTfm = 'net8.0'

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-PackageLibDirectory([string] $PackageDirectory) {
    # Package layouts differ; take the closest thing to the documented framework.
    $lib = Join-Path $PackageDirectory 'lib'
    if (-not (Test-Path $lib)) { return $null }

    foreach ($tfm in @($documentedTfm, 'net7.0', 'net6.0', 'netstandard2.1', 'netstandard2.0')) {
        $candidate = Join-Path $lib $tfm
        if (Test-Path $candidate) { return $candidate }
    }

    $fallback = Get-ChildItem $lib -Directory | Select-Object -First 1
    if ($fallback) { return $fallback.FullName }
    return $null
}

function Copy-Symbols($Package, [string] $Destination) {
    # Fetch the .snupkg on its own and stage its .pdb. Without it, docfx emits no view-source link.
    $fileName = '{0}.{1}.snupkg' -f $Package.Id, $Package.Version
    $archive = Join-Path $staging $fileName

    if (Test-Path -PathType Container $Feed) {
        $source = Join-Path $Feed $fileName
        if (-not (Test-Path $source)) { return $false }
        Copy-Item $source $archive
    }
    else {
        # nuget.org does NOT serve .snupkg from the flat container - not for any package. Symbol
        # packages go to symbols.nuget.org and are only retrievable through the symbol-server
        # protocol, keyed by the pdb signature inside the assembly rather than by id and version.
        # dotnet-symbol speaks that protocol, so it reads the staged dll and fetches the matching pdb.
        $symbolDirectory = Join-Path $staging ('symbols-' + $Package.Id)
        New-Item $symbolDirectory -ItemType Directory -Force | Out-Null

        dotnet dotnet-symbol `
            --server-path https://symbols.nuget.org/download/symbols `
            --symbols `
            --output $symbolDirectory `
            (Join-Path $Destination ($Package.Id + '.dll')) | Out-Null

        $fetched = Join-Path $symbolDirectory ($Package.Id + '.pdb')
        if (-not (Test-Path $fetched)) { return $false }

        Copy-Item $fetched $Destination -Force
        return $true
    }

    $expanded = Join-Path $staging ('symbols-' + $Package.Id)
    Expand-Archive -Path $archive -DestinationPath $expanded -Force
    Remove-Item $archive -Force

    $pdb = Join-Path $expanded 'lib' $documentedTfm ($Package.Id + '.pdb')
    if (-not (Test-Path $pdb)) { return $false }

    Copy-Item $pdb $Destination
    return $true
}

# --- The documented packages, read from docs.csproj so this script never holds a second list -----
$documented = Select-Xml -Path (Join-Path $root 'docs.csproj') -XPath '//PackageReference' |
    ForEach-Object { [pscustomobject]@{ Id = $_.Node.Include; Version = $_.Node.Version } }

if (-not $documented) { throw 'docs.csproj declares no PackageReference, so there is nothing to document.' }

$versionSummary = ($documented | ForEach-Object { '{0} {1}' -f $_.Id, $_.Version }) -join ', '
Write-Step ("Documenting $($documented.Count) packages: $versionSummary")

Write-Step 'Restoring tools'
dotnet tool restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

Write-Step "Restoring packages from $Feed"
dotnet restore (Join-Path $root 'docs.csproj') --packages $packages --source $Feed
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

# --- Stage what docfx reflects ------------------------------------------------------------------
Write-Step 'Staging assemblies, XML docs and symbols'
Remove-Item $staging -Recurse -Force -ErrorAction Ignore
New-Item $stagedApi, $stagedRefs -ItemType Directory -Force | Out-Null

$withoutSymbols = @()
foreach ($package in $documented) {
    $lib = Join-Path $packages $package.Id.ToLowerInvariant() $package.Version 'lib' $documentedTfm
    $assembly = Join-Path $lib ($package.Id + '.dll')
    $documentation = Join-Path $lib ($package.Id + '.xml')

    if (-not (Test-Path $assembly)) {
        throw ('{0} {1} has no {2} assembly at {3}.' -f $package.Id, $package.Version, $documentedTfm, $assembly)
    }

    if (-not (Test-Path $documentation)) {
        # A package built without GenerateDocumentationFile would produce a signature-only
        # reference: it looks finished and documents nothing. Refuse instead.
        throw ('{0} {1} ships no XML documentation file.' -f $package.Id, $package.Version)
    }

    Copy-Item $assembly, $documentation $stagedApi
    if (-not (Copy-Symbols -Package $package -Destination $stagedApi)) { $withoutSymbols += $package.Id }
}

Get-ChildItem $staging -Directory -Filter 'symbols-*' | Remove-Item -Recurse -Force

if ($withoutSymbols) {
    Write-Warning ('No symbols staged for: {0}. Those types get no view-source link.' -f ($withoutSymbols -join ', '))
}

# Every dependency of the documented assemblies, so docfx can resolve the types they expose.
foreach ($directory in Get-ChildItem $packages -Directory) {
    $version = Get-ChildItem $directory.FullName -Directory | Select-Object -First 1
    if (-not $version) { continue }

    $lib = Get-PackageLibDirectory $version.FullName
    if (-not $lib) { continue }

    foreach ($file in Get-ChildItem $lib -Filter '*.dll' -ErrorAction Ignore) {
        if ($documented.Id -contains [IO.Path]::GetFileNameWithoutExtension($file.Name)) { continue }
        Copy-Item $file.FullName $stagedRefs -Force -ErrorAction Ignore
    }
}

Write-Host ('    {0} documented assemblies, {1} reference assemblies' -f
    (Get-ChildItem $stagedApi -Filter '*.dll').Count,
    (Get-ChildItem $stagedRefs -Filter '*.dll').Count)

# --- The Examples section, generated from the Showroom chapters ---------------------------------
if (Test-Path (Join-Path $ShowroomPath 'TestFramework.Showroom.slnx')) {
    Write-Step 'Generating the Examples section'
    dotnet run --project (Join-Path $root 'tools' 'ShowroomDocs' 'ShowroomDocs.csproj') -c Release -- `
        --showroom $ShowroomPath `
        --out (Join-Path $root 'examples') `
        --captured (Join-Path $root 'showroom-output.json') `
        --measured (Join-Path $root 'run-data' 'showroom-measurements.json') `
        --allow-missing-narration ($AllowMissingNarration.IsPresent.ToString().ToLowerInvariant())

    if ($LASTEXITCODE -ne 0) { throw 'Examples generation failed.' }
}
else {
    Write-Warning "No Showroom at $ShowroomPath; the Examples section will be missing from this build."
}

# --- The version table the landing page includes ------------------------------------------------
Write-Step 'Writing the version table'
$includes = Join-Path $root 'guide' 'includes'
New-Item $includes -ItemType Directory -Force | Out-Null

$table = @('| Package | Version |', '|---|---|')
$table += $documented | ForEach-Object {
    '| [{0}](https://www.nuget.org/packages/{0}) | {1} |' -f $_.Id, $_.Version
}
Set-Content -Path (Join-Path $includes 'versions.md') -Value $table -Encoding utf8NoBOM

# --- Build --------------------------------------------------------------------------------------
Write-Step 'Running docfx'
$arguments = @((Join-Path $root 'docfx.json'), '--warningsAsErrors')
if ($Serve) { $arguments += '--serve' }

dotnet docfx @arguments
if ($LASTEXITCODE -ne 0) { throw 'docfx failed.' }
