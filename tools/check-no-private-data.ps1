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
      2. this script, called from .githooks/pre-commit, which additionally reads a WORD LIST of
         actual names and identifiers. That list is deliberately NOT in the repository: a file
         listing forbidden names would publish exactly what it is meant to protect.

    The word list lives at %LOCALAPPDATA%\KlangHub\private-words.txt, one entry per line, '#' for
    comments.

    Three faults found in this script itself during the same review, all fixed here:

      * -Staged read the files from disk. It listed what was staged and then checked the working
        tree, so staging a file with a real name in it and cleaning the file afterwards committed
        the private version past a green hook. It reads the staged blob now.
      * A missing word list printed a note and exited 0, reporting "No private data found" while the
        only layer that knows actual names was absent. In a fresh clone - where whoever is committing
        knows the conventions least - that was the whole safeguard gone, silently. It now refuses,
        unless -AllowMissingWordList says otherwise.
      * The description above claimed mailboxes and hardware addresses were checked. They were not,
        by either the extension list or any pattern. Had the hardware-address check existed, the
        soundbar's address would never have been committed in the first place.

.PARAMETER Staged
    Check only what is about to be committed (what the hook does). Otherwise everything git tracks.

.PARAMETER History
    Also check every blob reachable from any branch or tag. Slow, and meant for the moment before a
    first push: the shape checks and the word list otherwise only ever see the current state, which
    is exactly how a hardware address survived in three commits while every test was green.

.PARAMETER AllowMissingWordList
    Run the shape checks alone, and say so, instead of refusing. For a clone that has no list yet.

.EXAMPLE
    tools\check-no-private-data.ps1
    tools\check-no-private-data.ps1 -Staged
    tools\check-no-private-data.ps1 -History
#>
param(
    [switch]$Staged,
    [switch]$History,
    [switch]$AllowMissingWordList,
    [string]$WordListPath = (Join-Path $env:LOCALAPPDATA "KlangHub\private-words.txt")
)

$ErrorActionPreference = 'Stop'
$findings = @()

# --- the word list, first: without it this is not the check it claims to be -----------------------
$words = @()
if (Test-Path $WordListPath) {
    $words = Get-Content $WordListPath -Encoding UTF8 | ForEach-Object { $_.Trim() } |
             Where-Object { $_ -and -not $_.StartsWith('#') }
} elseif ($AllowMissingWordList) {
    Write-Output "note: no word list at $WordListPath - shape checks only, names are NOT checked."
} else {
    Write-Output ""
    Write-Output "REFUSED - there is no private word list at:"
    Write-Output "  $WordListPath"
    Write-Output ""
    Write-Output "That file holds the real names and identifiers this repository must never contain."
    Write-Output "It is deliberately kept outside the repository. Without it, only the SHAPE of private"
    Write-Output "data is checked, and no name would be caught - which is the one thing this is for."
    Write-Output ""
    Write-Output "Create it (one entry per line, '#' for comments), or pass -AllowMissingWordList to"
    Write-Output "run the shape checks alone and accept that names are unchecked."
    exit 1
}

# --- which files, and how their content is read ---------------------------------------------------
# The content always comes from what would actually be committed, never from the working tree.
if ($History) {
    $files = @()
} elseif ($Staged) {
    $files = git diff --cached --name-only --diff-filter=ACMR
} else {
    $files = git ls-files
}
$files = $files | Where-Object { $_ }

function Get-CheckedContent([string]$path) {
    if ($Staged) {
        # The staged blob, not the file on disk. Staging a file and then cleaning it used to commit
        # the private version past a hook that reported nothing wrong.
        $text = git show ":$path" 2>$null
    } elseif (Test-Path $path -PathType Leaf) {
        $text = Get-Content $path -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    } else {
        return $null
    }
    if ($text -is [array]) { $text = $text -join "`n" }
    return $text
}

# --- what a name, an address or a mailbox looks like ----------------------------------------------
# Kept in step with Source/KlangHub.Tests/RepositoryPrivacyTests.cs. The description of this script
# has always claimed these; until now only the test actually had them.
$patterns = @(
    @{ Name = 'a mail address';
       Rx   = '(?<![A-Za-z0-9._%+-])[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}';
       Ok   = '(noreply|no-reply)|@(example\.(com|org|net|invalid)|users\.noreply\.github\.com)$|\.dll$' },
    @{ Name = 'a home directory (write %USERPROFILE% instead)';
       Rx   = '([A-Za-z]:[\\/]|/)(Users|home)[\\/](?!<|%|\{|\$)[A-Za-z0-9._-]+';
       Ok   = '(Users|home)[\\/](Public|All Users|Default|runner|root)\b' },
    @{ Name = 'a hardware address';
       Rx   = '(?<![0-9A-Fa-f:])([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}(?![0-9A-Fa-f:])';
       Ok   = '^(00:00:00:00:00:00|00:11:22:33:44:55|A4:B1:C2:D3:E4:F5|AA:BB:CC:DD:EE:FF|DE:AD:BE:EF:00:01)$' }
)

$forbiddenExtensions = @('.png', '.jpg', '.jpeg', '.gif', '.bmp', '.pdf', '.log', '.dmp', '.pcap',
                         '.pcapng', '.har', '.etl', '.zip', '.7z', '.rar', '.mp4', '.mov')

# The application's own artwork, and nothing else. This used to exempt the whole of Source/ and the
# whole of installer/, so a screenshot one directory deeper than the ones that were found would have
# passed all three layers untouched.
$shippedAssets = @(
    '^Source/KlangHub/KlangHub\.ico$',
    '^Source/KlangHub/Resources/artwork\.png$',
    '^Source/KlangHub/UserControls/[A-Za-z]+\.png$',
    '^installer/wizard-(small|large)(-\d+)?\.bmp$',
    '^receiver/assets/[a-z]+\.png$'
)

$textExtensions = @('.cs', '.md', '.txt', '.html', '.css', '.js', '.json', '.xml', '.resx', '.csproj',
                    '.props', '.targets', '.ps1', '.psm1', '.psd1', '.iss', '.proto', '.config',
                    '.yml', '.yaml', '.gitignore', '.editorconfig', '.sln', '.xlf', '.sh', '.bat',
                    '.cmd', '.py', '.toml', '.ini', '.csv', '.svg')

# This file and the test describe the patterns, so they contain examples of them by necessity.
$selfDescribing = 'check-no-private-data\.ps1$|RepositoryPrivacyTests\.cs$|^CLAUDE\.md$|^\.gitignore$'

function Test-OneFile([string]$path, [string]$content) {
    $local = @()

    $ext = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
    $isAsset = $false
    foreach ($rx in $shippedAssets) { if ($path -match $rx) { $isAsset = $true; break } }

    if ($forbiddenExtensions -contains $ext -and -not $isAsset) {
        $local += "$path  - a screenshot, capture, archive or log; none of these belong in a public repository"
    }

    if ($null -eq $content) { return $local }
    if ($textExtensions -notcontains $ext -and $ext -ne '') { return $local }
    if ($path -match $selfDescribing) { return $local }

    foreach ($w in $words) {
        if ($content -match [regex]::Escape($w)) {
            # The finding names the file, never the word - this output can end up in a log.
            $local += "$path  - contains an entry from the private word list"
            break
        }
    }

    foreach ($p in $patterns) {
        foreach ($hit in ([regex]$p.Rx).Matches($content)) {
            if ($hit.Value -notmatch $p.Ok) {
                $local += "$path  - contains $($p.Name)"
                break
            }
        }
    }

    return $local
}

# --- the current state ----------------------------------------------------------------------------
$checked = 0
foreach ($f in $files) {
    $content = Get-CheckedContent $f
    $findings += Test-OneFile $f $content
    $checked++
}

# --- and, on request, everything that was ever committed -------------------------------------------
if ($History) {
    Write-Output "Reading every blob reachable from a local branch or tag. This takes a while."

    # Commit metadata first: no layer has ever looked at it, which is how commits carrying a real
    # name and mail address came to be pushed without anything objecting.
    #
    # --branches --tags, not --all: a remote-tracking ref is somebody else's history, not something
    # this repository is about to publish, and including them buried the findings that matter under
    # the whole of the upstream project.
    #
    # Whoever signed a commit before this became a fork keeps their own identity: the licence asks
    # for that attribution, and it was already public in the project we forked from. Everything since
    # must be a noreply address.
    $upstreamAuthors = @(
        'SamDel <github@deaut.nl>',
        'SamDel <25846417+SamDel@users.noreply.github.com>'
    )
    # The address is followed by '>' in the formatted line, so the anchor allows for it.
    $allowedAuthors = '@users\.noreply\.github\.com>?$'
    $authors = git log --branches --tags --format='%an <%ae>%n%cn <%ce>' | Sort-Object -Unique
    foreach ($a in $authors) {
        if ($a -notmatch $allowedAuthors -and $upstreamAuthors -notcontains $a) {
            $findings += "commit metadata  - an author or committer is not a noreply address: $($a -replace '[A-Za-z0-9._%+-]+@','***@')"
        }
    }

    $objects = git rev-list --objects --branches --tags
    foreach ($line in $objects) {
        $sha, $path = $line -split ' ', 2
        if (-not $path) { continue }

        $type = git cat-file -t $sha 2>$null
        if ($type -ne 'blob') { continue }

        $ext = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
        $content = $null
        if ($textExtensions -contains $ext -or $ext -eq '') {
            $content = git cat-file -p $sha 2>$null
            if ($content -is [array]) { $content = $content -join "`n" }
        }

        foreach ($finding in (Test-OneFile $path $content)) {
            $findings += "$finding  [in history, blob $($sha.Substring(0,8))]"
        }
        $checked++
    }

    foreach ($w in $words) {
        $touching = git log --branches --tags --format='%h' -S"$w" 2>$null
        if ($touching) {
            $findings += "history  - $(@($touching).Count) commit(s) add or remove an entry from the private word list: $($touching -join ', ')"
        }
        $inMessages = git log --branches --tags --format='%h' --grep="$w" -i 2>$null
        if ($inMessages) {
            $findings += "commit messages  - $(@($inMessages).Count) message(s) contain an entry from the private word list: $($inMessages -join ', ')"
        }
    }
}

# --- verdict --------------------------------------------------------------------------------------
if ($findings.Count -gt 0) {
    Write-Output ""
    Write-Output "REFUSED - this would put private data into a public repository:"
    Write-Output ""
    $findings | Select-Object -Unique | ForEach-Object { Write-Output "  $_" }
    Write-Output ""
    Write-Output "Fix it, or if a finding is wrong, say why in the commit and use --no-verify."
    exit 1
}

Write-Output "No private data found in $checked item(s)."
exit 0
