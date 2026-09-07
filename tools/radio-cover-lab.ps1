<#
.SYNOPSIS
    Runs the radio cover lab: the whole chain from an ICY line to a picture on the stage.

.DESCRIPTION
    Without -Live the lab runs against the frozen answers under
    Source/KlangHub.Tests/Fixtures/musicbrainz and touches no network at all.

    With -Live it additionally asks musicbrainz.org and coverartarchive.org, one polite
    question at a time, and reports where the frozen answers have drifted away from what
    the archive serves today.

.EXAMPLE
    tools\radio-cover-lab.ps1
    tools\radio-cover-lab.ps1 -Live
#>
[CmdletBinding()]
param(
    [switch] $Live
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'Source\KlangHub.Tests\KlangHub.Tests.csproj'
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'

if (-not (Test-Path $dotnet)) {
    throw "No SDK at $dotnet. The dotnet on PATH is a runtime without one."
}

if ($Live) {
    Write-Host 'Asking musicbrainz.org and coverartarchive.org - one question per 1.2 s.' -ForegroundColor Yellow
    $env:KLANGHUB_COVER_LAB_LIVE = '1'
} else {
    Write-Host 'Frozen answers only. Add -Live to ask the catalogue itself.' -ForegroundColor DarkGray
    $env:KLANGHUB_COVER_LAB_LIVE = '0'
}

try {
    & $dotnet test $project
    exit $LASTEXITCODE
} finally {
    Remove-Item Env:\KLANGHUB_COVER_LAB_LIVE -ErrorAction SilentlyContinue
}
