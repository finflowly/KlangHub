# Builds Source/KlangHub/KlangHub.ico - the icon Windows shows in the title bar, the taskbar, the tray
# and the Task Manager.
#
# Why a generator and not a single drawing scaled down: the previous icon was one 256-px artwork
# (a dark rounded tile with three concentric amber rings) resampled to every smaller size. At 16 and
# 20 px - the sizes the Task Manager and the tray actually use - the rings blur into each other and
# the tile goes dark and featureless. Observed exactly that on a real Start menu: "ein Icon ohne Inhalt".
#
# So each size is drawn for itself. Small sizes drop the concentric rings and keep one bold amber ring
# plus a solid centre, thick enough to survive at 16 px; from 48 px up the full three-ring mark returns.
# Sizes 16/20/24/32/40/48 cover the DPI scalings Windows asks for, which is why 20 and 40 are here.
#
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File tools\make-app-icon.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Theme.cs is the single source of truth for these; keep them in step.
$Ink    = [System.Drawing.Color]::FromArgb(0x0E, 0x10, 0x14)
$Amber  = [System.Drawing.Color]::FromArgb(0xE8, 0xB6, 0x5A)
$Dim    = [System.Drawing.Color]::FromArgb(0xB9, 0x8B, 0x3E)

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $r = [Math]::Min($r, [Math]::Min($w, $h) / 2)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($r -le 0) { $p.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $w, $h)); $p.CloseFigure(); return $p }
    $d = $r * 2
    $p.AddArc($x,          $y,          $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y,          $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d,   0, 90)
    $p.AddArc($x,          $y + $h - $d, $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # The dark tile. At small sizes it takes the whole square with a tighter corner, so no pixels are
    # spent on a margin that would only shrink the mark further.
    $inset  = if ($size -ge 48) { $size * 0.055 } else { 0.0 }
    $radius = if ($size -ge 48) { $size * 0.22  } else { $size * 0.18 }
    $tile = New-RoundedPath $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) $radius
    $brush = New-Object System.Drawing.SolidBrush $Ink
    $g.FillPath($brush, $tile)
    $brush.Dispose(); $tile.Dispose()

    $c = $size / 2.0

    if ($size -ge 48) {
        # The full mark: outer ring, mid ring, solid centre - the design as it was conceived.
        $ringW = [Math]::Max(1.0, $size * 0.055)
        $pen = New-Object System.Drawing.Pen $Dim, $ringW
        $r1 = $size * 0.335
        $g.DrawEllipse($pen, ($c - $r1), ($c - $r1), (2 * $r1), (2 * $r1))
        $pen.Dispose()

        $pen = New-Object System.Drawing.Pen $Amber, $ringW
        $r2 = $size * 0.215
        $g.DrawEllipse($pen, ($c - $r2), ($c - $r2), (2 * $r2), (2 * $r2))
        $pen.Dispose()

        $b = New-Object System.Drawing.SolidBrush $Amber
        $r3 = $size * 0.095
        $g.FillEllipse($b, ($c - $r3), ($c - $r3), (2 * $r3), (2 * $r3))
        $b.Dispose()
    }
    else {
        # One ring and a centre. Both are deliberately heavy: at 16 px a hairline ring disappears into
        # the tile, which is how the old icon ended up looking empty.
        $ringW = [Math]::Max(1.5, $size * 0.115)
        $pen = New-Object System.Drawing.Pen $Amber, $ringW
        $r1 = ($size * 0.30) - ($ringW / 2)
        $g.DrawEllipse($pen, ($c - $r1), ($c - $r1), (2 * $r1), (2 * $r1))
        $pen.Dispose()

        $b = New-Object System.Drawing.SolidBrush $Amber
        $r3 = [Math]::Max(1.0, $size * 0.105)
        $g.FillEllipse($b, ($c - $r3), ($c - $r3), (2 * $r3), (2 * $r3))
        $b.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# A 32-bit BGRA DIB with the AND mask an ICO directory entry expects. Only 128 and 256 px are stored as
# PNG - those are the sizes where PNG compression is universally understood, and where a raw DIB would
# cost 64 KB apiece. The small sizes stay DIB: that is what every consumer of them reads, and some read
# nothing else.
function ConvertTo-Dib([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$w); $bw.Write([int]($h * 2))   # height counts colour + mask
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
    $bw.Write([int]($w * $h * 4)); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $h - 1; $y -ge 0; $y--) {                                 # DIB rows run bottom-up
        for ($x = 0; $x -lt $w; $x++) {
            $p = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$p.B); $bw.Write([byte]$p.G); $bw.Write([byte]$p.R); $bw.Write([byte]$p.A)
        }
    }
    $stride = [int](([Math]::Floor(($w + 31) / 32)) * 4)                # AND mask, 1 bpp, 4-byte rows
    for ($y = 0; $y -lt $h; $y++) { for ($i = 0; $i -lt $stride; $i++) { $bw.Write([byte]0) } }
    $bw.Flush()
    return ,$ms.ToArray()      # the comma stops PowerShell unrolling the array into loose bytes
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 128) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += , @{ Size = $s; Data = $ms.ToArray(); Png = $true }
    }
    else {
        $frames += , @{ Size = $s; Data = (ConvertTo-Dib $bmp); Png = $false }
    }
    $bmp.Dispose()
}

$target = Join-Path $PSScriptRoot '..\Source\KlangHub\KlangHub.ico'
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $w.Write([byte]($(if ($f.Size -ge 256) { 0 } else { $f.Size })))
    $w.Write([byte]($(if ($f.Size -ge 256) { 0 } else { $f.Size })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$f.Data.Length); $w.Write([int]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f.Data) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path $target), $out.ToArray())
"KlangHub.ico geschrieben: $($frames.Count) Groessen, $($out.Length) Bytes"
