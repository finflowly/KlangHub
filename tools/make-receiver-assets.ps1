# Renders the Styled Media Receiver artwork in KlangHub's own palette (same tokens as Classes/Theme.cs).
# Four assets, all served from GitHub Pages next to the CSS:
#   background.png  - behind the receiver while media plays
#   splash.png      - the idle screen
#   logo.png        - shown while the receiver launches (transparent)
#   watermark.png   - small mark kept on screen during playback (transparent)
Add-Type -AssemblyName System.Drawing

$out = "C:\Code\KlangHub\receiver\assets"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$Ink   = [System.Drawing.Color]::FromArgb(14, 16, 20)
$Ink2  = [System.Drawing.Color]::FromArgb(11, 13, 17)
$Amber = [System.Drawing.Color]::FromArgb(232, 182, 90)
$Ivory = [System.Drawing.Color]::FromArgb(243, 236, 221)

function Draw-BrandMark($g, [single]$cx, [single]$cy, [single]$r, [int]$glowAlpha = 95) {
  $glow = New-Object System.Drawing.Drawing2D.GraphicsPath
  $glow.AddEllipse(($cx - $r * 1.6), ($cy - $r * 1.6), ($r * 3.2), ($r * 3.2))
  $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow)
  $pgb.CenterColor = [System.Drawing.Color]::FromArgb($glowAlpha, $Amber)
  $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $Amber))
  $g.FillPath($pgb, $glow); $pgb.Dispose(); $glow.Dispose()

  $p1 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, $Amber), [single]($r * 0.09))
  $g.DrawEllipse($p1, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2)); $p1.Dispose()
  $p2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, $Amber), [single]($r * 0.09))
  $g.DrawEllipse($p2, ($cx - $r * 0.6), ($cy - $r * 0.6), ($r * 1.2), ($r * 1.2)); $p2.Dispose()
  $core = New-Object System.Drawing.SolidBrush($Amber)
  $g.FillEllipse($core, ($cx - $r * 0.27), ($cy - $r * 0.27), ($r * 0.54), ($r * 0.54)); $core.Dispose()
}

function Draw-Rings($g, [int]$w, [int]$h, [single]$cx, [single]$cy, [single]$step, [int]$count) {
  for ($i = 1; $i -le $count; $i++) {
    $r = $step * $i
    $a = 34 - $i * 2
    if ($a -lt 4) { $a = 4 }
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($a, $Amber), 1.7)
    $g.DrawEllipse($pen, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2)); $pen.Dispose()
  }
}

# --- background: quiet, dark, nothing that fights the receiver's own text -------------------------
function Save-Background([string]$path, [bool]$withWordmark) {
  $w = 1920; $h = 1080
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'ClearTypeGridFit'

  $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $Ink2, $Ink, 90)
  $g.FillRectangle($bg, $rect); $bg.Dispose()

  # the ring band sits off-centre so the receiver's metadata (lower left) stays on calm ground
  Draw-Rings $g $w $h ($w * 0.72) ($h * 0.34) ($h * 0.085) 11
  Draw-BrandMark $g ($w * 0.72) ($h * 0.34) ($h * 0.055) 70

  if ($withWordmark) {
    $nameFont = New-Object System.Drawing.Font("Segoe UI Semibold", [single]($h * 0.075), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $tagFont = New-Object System.Drawing.Font("Segoe UI", [single]($h * 0.024), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $nb = New-Object System.Drawing.SolidBrush($Ivory)
    $tb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, $Amber))
    $g.DrawString("KlangHub", $nameFont, $nb, [single]($w * 0.08), [single]($h * 0.42))
    $g.DrawString("LOSSLESS WHOLE-HOME AUDIO", $tagFont, $tb, [single]($w * 0.083), [single]($h * 0.52))
    $nameFont.Dispose(); $tagFont.Dispose(); $nb.Dispose(); $tb.Dispose()
  }

  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "SAVED $path ${w}x${h}"
}

# --- logo: shown while the receiver launches, on transparency ------------------------------------
function Save-Logo([string]$path) {
  $w = 900; $h = 520
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'ClearTypeGridFit'
  $g.Clear([System.Drawing.Color]::Transparent)

  Draw-Rings $g $w $h ($w * 0.5) ($h * 0.34) ($h * 0.07) 5
  Draw-BrandMark $g ($w * 0.5) ($h * 0.34) ($h * 0.10)

  $nameFont = New-Object System.Drawing.Font("Segoe UI Semibold", [single]($h * 0.17), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = 'Center'
  $nb = New-Object System.Drawing.SolidBrush($Ivory)
  $r = New-Object System.Drawing.RectangleF(0, ($h * 0.62), $w, ($h * 0.25))
  $g.DrawString("KlangHub", $nameFont, $nb, $r, $sf)
  $nameFont.Dispose(); $nb.Dispose(); $sf.Dispose()

  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "SAVED $path ${w}x${h}"
}

# --- watermark: the mark alone, discreet, for the corner during playback --------------------------
function Save-Watermark([string]$path) {
  $s = 260
  $bmp = New-Object System.Drawing.Bitmap($s, $s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)
  Draw-BrandMark $g ($s * 0.5) ($s * 0.5) ($s * 0.30) 60
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "SAVED $path ${s}x${s}"
}

Save-Background "$out\background.png" $false
Save-Background "$out\splash.png" $true
Save-Logo "$out\logo.png"
Save-Watermark "$out\watermark.png"
