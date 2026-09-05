<#
.SYNOPSIS
    Refuses a commit that would put private data into this public repository.

.DESCRIPTION
    This repository is public. Everything committed to it is readable by anyone, permanently, and
    rewriting history afterwards only helps as long as nothing has been pushed.

    A review on 2026-09-05 found, none of it deliberate: a real name in the copyright inside the
    shipped executable, a home directory in the documentation, a self-chosen room name, a soundbar's
    actual hardware address in three test files, and eight screenshots tracked in the repository root
    showing a local IP address, a list of speakers and the audio hardware in a machine. It had simply
    accumulated - which is why remembering is not a safeguard.

    Two layers guard against it:

      1. Source/KlangHub.Tests/RepositoryPrivacyTests.cs - runs with every test run and checks the
         SHAPE of private data: mailboxes, home directories, hardware addresses, screenshots.
      2. this script, called from .git/hooks/pre-commit, which additionally reads a WORD LIST of
         actual names and identifiers. That list is deliberately NOT in the repository: a file
         listing forbidden names would publish exactly what it is meant to protect.

    The word list lives at %LOCALAPPDATA%\KlangHub\private-words.txt, one entry per line, '#' for
    comments. Without it this script still runs the shape checks.

.PARAMETER Staged
    Check only what is about to be committed (what the hook does). Otherwise everything git tracks.

.EXAMPLE
    tools\check-no-private-data.ps1
    tools\check-no-private-data.ps1 -Staged
#>
param(
    [switch]$Staged,
    [string]$WordListPath = (Join-Path $env:LOCALAPPDATA "KlangHub\private-words.txt")
)

$ErrorActionPreference = 'Stop'
$findings = @()

# --- which files ---------------------------------------------------------------------------------
if ($Staged) {
    $files = git diff --cached --name-only --diff-filter=ACMR
} else {
    $files = git ls-files
}
$files = $files | Where-Object { $_ -and (Test-Path $_ -PathType Leaf) }

if (-not $files) { Write-Output "Nothing to check."; exit 0 }

# --- 1. file types that carry more than they show ------------------------------------------------
$forbiddenExtensions = @('.png', '.jpg', '.jpeg', '.gif', '.bmp', '.pdf', '.log')
foreach ($f in $files) {
    $ext = [System.IO.Path]::GetExtension($f).ToLowerInvariant()
    if ($forbiddenExtensions -contains $ext -and $f -notmatch '^(Source|installer|receiver/assets)/') {
        $findings += "$f  - a screenshot, log or PDF outside the application's own assets"
    }
}

# --- 2. the word list, when there is one ---------------------------------------------------------
$words = @()
if (Test-Path $WordListPath) {
    $words = Get-Content $WordListPath | ForEach-Object { $_.Trim() } |
             Where-Object { $_ -and -not $_.StartsWith('#') }
} else {
    Write-Output "note: no word list at $WordListPath - running shape checks only."
    Write-Output "      Put real names and identifiers there, one per line. It is never committed."
}

# --- 3. shapes ------------------------------------------------------------------------------------
$textExtensions = @('.cs', '.md', '.txt', '.html', '.css', '.js', '.json', '.xml', '.resx', '.csproj',
                    '.props', '.targets', '.ps1', '.psm1', '.iss', '.proto', '.config', '.yml', '.yaml',
                    '.gitignore', '.editorconfig', '.sln')

foreach ($f in $files) {
    $ext = [System.IO.Path]::GetExtension($f).ToLowerInvariant()
    if ($textExtensions -notcontains $ext -and $ext -ne '') { continue }
    if ($f -match 'check-no-private-data\.ps1$|RepositoryPrivacyTests\.cs$|^CLAUDE\.md$|^\.gitignore$') { continue }
    if ((Get-Item $f).Length -gt 2MB) { continue }

    $content = Get-Content $f -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    foreach ($w in $words) {
        if ($content -match [regex]::Escape($w)) {
            # The finding names the file, never the word - this output can end up in a log.
            $findings += "$f  - contains an entry from the private word list"
            break
        }
    }

    if ($content -match '[A-Za-z]:\\Users\\(?!<|%|\{)[A-Za-z0-9._-]+') {
        $findings += "$f  - names a home directory (write %USERPROFILE% instead)"
    }
}

# --- verdict --------------------------------------------------------------------------------------
if ($findings.Count -gt 0) {
    Write-Output ""
    Write-Output "COMMIT REFUSED - this would put private data into a public repository:"
    Write-Output ""
    $findings | Select-Object -Unique | ForEach-Object { Write-Output "  $_" }
    Write-Output ""
    Write-Output "Fix it, or if a finding is wrong, say why in the commit and use --no-verify."
    exit 1
}

Write-Output "No private data found in $($files.Count) file(s)."
exit 0
