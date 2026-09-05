<#
.SYNOPSIS
    Reads a KlangHub device log and reports whether a listening session was actually steady.

.DESCRIPTION
    A stability run is only worth anything if "it sounded fine" can be replaced by numbers. This walks the
    log KlangHub writes while "Geräte-Protokoll aufzeichnen" is on and reports, per device:

      - reconnects        a session that had to be built again. The one number that matters most: a
                          listener hears every single one of these as a gap.
      - launches          how often the receiver application was started. More than one per session means
                          something is knocking it over.
      - errors            LOAD_FAILED, LAUNCH_ERROR, INVALID_REQUEST, connection errors.
      - silent gaps       stretches with no traffic at all from a device that should have been talking.
                          A Cast device pings every few seconds; a gap of 15 s means it stopped answering.

    A run is clean when reconnects, errors and gaps are all zero for every device.

.EXAMPLE
    tools\analyse-device-log.ps1
    tools\analyse-device-log.ps1 -Path "$env:LOCALAPPDATA\KlangHub\logs\klanghub-2026-09-05.log" -GapSeconds 15
#>
param(
    [string]$Path,
    [int]$GapSeconds = 15,
    [switch]$ShowGaps
)

if (-not $Path) {
    $folder = Join-Path $env:LOCALAPPDATA "KlangHub\logs"
    if (-not (Test-Path $folder)) {
        Write-Output "No log folder yet: $folder"
        Write-Output "Turn on Einstellungen -> 'Geräte-Protokoll aufzeichnen' and cast something."
        exit 1
    }
    $Path = (Get-ChildItem $folder -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if (-not (Test-Path $Path)) { Write-Output "Not found: $Path"; exit 1 }

# Read while KlangHub still has it open - the usual case, since a run is analysed right after it.
$stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
$reader = New-Object System.IO.StreamReader($stream)
$lines = $reader.ReadToEnd() -split "`r?`n" | Where-Object { $_ -ne '' }
$reader.Dispose(); $stream.Dispose()

Write-Output "Log:    $Path"
Write-Output "Lines:  $($lines.Count)"

$devices = @{}
$firstTime = $null
$lastTime = $null

function Get-Stamp($line) {
    if ($line -match '^(\d{2}):(\d{2}):(\d{2})\.(\d{3}) ') {
        return [TimeSpan]::new(0, [int]$Matches[1], [int]$Matches[2], [int]$Matches[3], [int]$Matches[4])
    }
    return $null
}

foreach ($line in $lines) {
    $t = Get-Stamp $line
    if ($null -eq $t) { continue }
    if ($null -eq $firstTime) { $firstTime = $t }
    $lastTime = $t

    if ($line -notmatch '\[(\d{1,3}(?:\.\d{1,3}){3}):(\d+)\]') { continue }
    $host_ = $Matches[1]

    if (-not $devices.ContainsKey($host_)) {
        $devices[$host_] = [pscustomobject]@{
            Host = $host_; Lines = 0; Reconnects = 0; Launches = 0; Errors = 0
            Gaps = @(); Last = $t; First = $t; ErrorLines = @(); Drops = @(); ErrorCodes = @{}
            WasPlaying = $false; Positions = @()
        }
    }
    $d = $devices[$host_]
    $d.Lines++

    $delta = ($t - $d.Last).TotalSeconds
    if ($delta -lt 0) { $delta += 86400 }   # crossed midnight
    # Only while it was playing. An idle device is polled every 15 s and answers every 15 s: counting
    # that as a silent gap buries the real ones under a page of noise.
    if ($delta -ge $GapSeconds -and $d.WasPlaying) {
        $d.Gaps += [pscustomobject]@{ At = $d.Last.ToString('hh\:mm\:ss'); Seconds = [math]::Round($delta, 1) }
    }
    $d.Last = $t
    $d.WasPlaying = ($line -match '\[(Playing|Buffering)\]')

    # How far the device has actually got. Falling behind real time is the earliest sign of trouble -
    # it shows up before any error does, and it is what a listener hears as the sound thinning out.
    if ($line -match '"playerState":"PLAYING","currentTime":([\d.]+)') {
        $d.Positions += [pscustomobject]@{ Clock = $t; Position = [double]$Matches[1] }
    }

    if ($line -match '"type":"LAUNCH"')        { $d.Launches++ }

    # A CLOSE while the device is merely being discovered is how Cast status polling works and means
    # nothing. Only a session that had to be rebuilt while music was playing is a reconnect - that is
    # the one a listener hears.
    if ($line -match 'ResumeAfterConnectionLoss|ResumePlaying') { $d.Reconnects++ }
    elseif ($line -match '"type":"CLOSE"' -and $line -match '\[(Playing|Buffering|Paused|LoadingMedia)\]') { $d.Reconnects++ }
    if ($line -match 'LOAD_FAILED|LAUNCH_ERROR|INVALID_REQUEST|ConnectError|ex :|"type":"ERROR"') {
        $d.Errors++
        if ($d.ErrorLines.Count -lt 5) { $d.ErrorLines += $line }
    }

    # The decisive line for a dropout: the receiver hung up on the audio stream. How long it lasted and
    # how much it swallowed before doing so is what tells a codec problem from a buffer one.
    if ($line -match 'Connection closed from .* after ([\d.,]+) MB in (\d+) s') {
        $mb = [double]($Matches[1] -replace ',', '.')
        $secs = [int]$Matches[2]
        $d.Drops += [pscustomobject]@{
            At = $t.ToString('hh\:mm\:ss'); MB = $mb; Seconds = $secs
            MbitPerSecond = if ($secs -gt 0) { [math]::Round($mb * 8 / $secs, 2) } else { 0 }
        }
    }

    # Cast's own media error codes say what the device thought was wrong.
    if ($line -match 'detailedErrorCode"?:\s*(\d+)') {
        $code = [int]$Matches[1]
        $d.ErrorCodes[$code] = 1 + $(if ($d.ErrorCodes.ContainsKey($code)) { $d.ErrorCodes[$code] } else { 0 })
    }
}

function Get-CastErrorName($code) {
    switch ($code) {
        100 { "MEDIA_UNKNOWN - the receiver could not say what went wrong" }
        101 { "MEDIA_ABORTED - playback was abandoned" }
        102 { "MEDIA_DECODE - the receiver could not decode the stream" }
        103 { "MEDIA_NETWORK - the receiver lost the stream over the network" }
        104 { "MEDIA_SRC_NOT_SUPPORTED - the receiver refuses this format outright" }
        default { "error code $code" }
    }
}

if ($devices.Count -eq 0) { Write-Output "No device traffic in this log."; exit 0 }

$span = $lastTime - $firstTime
if ($span.TotalSeconds -lt 0) { $span = $span.Add([TimeSpan]::FromDays(1)) }
Write-Output "Span:   $($firstTime.ToString('hh\:mm\:ss')) - $($lastTime.ToString('hh\:mm\:ss'))  ($([math]::Round($span.TotalMinutes,1)) min)"
Write-Output ""

$clean = $true
foreach ($d in $devices.Values | Sort-Object Host) {
    $verdict = if ($d.Reconnects -eq 0 -and $d.Errors -eq 0 -and $d.Gaps.Count -eq 0 -and $d.Drops.Count -eq 0) { "steady" } else { "NOT steady" }
    if ($verdict -ne "steady") { $clean = $false }

    Write-Output ("{0,-16} {1,6} lines | {2} reconnects | {3} launches | {4} errors | {5} gaps >= {6}s  -> {7}" -f `
        $d.Host, $d.Lines, $d.Reconnects, $d.Launches, $d.Errors, $d.Gaps.Count, $GapSeconds, $verdict)

    if ($ShowGaps -and $d.Gaps.Count -gt 0) {
        foreach ($g in $d.Gaps) { Write-Output ("      silent {0,6}s starting {1}" -f $g.Seconds, $g.At) }
    }

    # Compare elapsed playback against elapsed wall-clock between consecutive reports. A healthy device
    # gains one second of music per second of time; anything materially less is a stumble.
    $stumbles = @()
    for ($i = 1; $i -lt $d.Positions.Count; $i++) {
        $a = $d.Positions[$i - 1]; $b = $d.Positions[$i]
        $wall = ($b.Clock - $a.Clock).TotalSeconds
        $played = $b.Position - $a.Position
        # A restart resets the position to zero; that is a drop, already counted, not a stumble.
        if ($wall -le 0 -or $played -lt 0) { continue }
        if ($wall -ge 5 -and $played -lt ($wall - 1.5)) {
            $stumbles += [pscustomobject]@{
                At = $a.Clock.ToString('hh\:mm\:ss')
                Lost = [math]::Round($wall - $played, 1)
                Over = [math]::Round($wall, 0)
            }
        }
    }
    if ($stumbles.Count -gt 0) {
        Write-Output "      fell behind real time:"
        foreach ($x in $stumbles) { Write-Output ("        {0}  lost {1}s over {2}s" -f $x.At, $x.Lost, $x.Over) }
    }

    if ($d.Drops.Count -gt 0) {
        Write-Output "      stream dropped by the receiver:"
        foreach ($x in $d.Drops) {
            Write-Output ("        {0}  lasted {1,4}s, {2,6} MB, {3} Mbit/s" -f $x.At, $x.Seconds, $x.MB, $x.MbitPerSecond)
        }
        # Same length every time means something systematic - a buffer, a limit, a timeout. Wildly
        # different lengths point at the network or at particular content instead.
        $spread = ($d.Drops | Measure-Object -Property Seconds -Maximum -Minimum)
        if ($d.Drops.Count -gt 1) {
            Write-Output ("        shortest {0}s, longest {1}s" -f $spread.Minimum, $spread.Maximum)
        }
    }

    foreach ($code in $d.ErrorCodes.Keys) {
        Write-Output ("      x{0} {1}" -f $d.ErrorCodes[$code], (Get-CastErrorName $code))
    }
    foreach ($e in $d.ErrorLines) { Write-Output "      $e" }
}

Write-Output ""
Write-Output $(if ($clean) { "RESULT: clean run - no reconnects, no errors, no silent gaps." }
               else { "RESULT: not clean. The lines above say where to look." })
