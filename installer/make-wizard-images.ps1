# Draws the setup wizard's artwork in the app's own palette and with the complete brand mark - drawn, not
# cropped, so the logo is never cut by a panel edge. Same tokens as Classes/Theme.cs: ink, amber, ivory.
Add-Type -AssemblyName System.Drawing

$out = "C:\Code\KlangHub\installer"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$Ink   = [System.Drawing.Color]::FromArgb(14, 16, 20)      # #0E1014
$Ink2  = [System.Drawing.Color]::FromArgb(11, 13, 17)      # #0B0D11
$Amber = [System.Drawing.Color]::FromArgb(232, 182, 90)    # #E8B65A
$Ivory = [System.Drawing.Color]::FromArgb(243, 236, 221)   # #F3ECDD

function Draw-BrandMark($g, [single]$cx, [single]$cy, [single]$r) {
  # glow first, then the two rings and the core - the construction from Theme.DrawBrandMark
  $glow = New-Object System.Drawing.Drawing2D.GraphicsPath
  $glow.AddEllipse(($cx - $r * 1.5), ($cy - $r * 1.5), ($r * 3), ($r * 3))
  $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow)
  $pgb.CenterColor = [System.Drawing.Color]::FromArgb(95, $Amber)
  $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $Amber))
  $g.FillPath($pgb, $glow)
  $pgb.Dispose(); $glow.Dispose()

  $p1 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, $Amber), [single]($r * 0.09))
  $g.DrawEllipse($p1, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2)); $p1.Dispose()
  $p2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, $Amber), [single]($r * 0.09))
  $g.DrawEllipse($p2, ($cx - $r * 0.6), ($cy - $r * 0.6), ($r * 1.2), ($r * 1.2)); $p2.Dispose()
  $core = New-Object System.Drawing.SolidBrush($Amber)
  $g.FillEllipse($core, ($cx - $r * 0.27), ($cy - $r * 0.27), ($r * 0.54), ($r * 0.54)); $core.Dispose()
}

function Draw-Background($g, [int]$w, [int]$h) {
  $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $Ink2, $Ink, 90)
  $g.FillRectangle($bg, $rect); $bg.Dispose()
  # the quiet ring band the app shows behind its cards
  $cx = $w * 0.5; $cy = $h * 0.34
  for ($i = 1; $i -le 7; $i++) {
    $r = $h * 0.10 * $i
    $a = 30 - $i * 3
    if ($a -lt 5) { $a = 5 }
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($a, $Amber), [single]($h / 300))
    $g.DrawEllipse($pen, ($cx - $r), ($cy - $r), ($r * 2), ($r * 2)); $pen.Dispose()
  }
}

function Save-Large([int]$w, [int]$h, [string]$path) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'ClearTypeGridFit'
  Draw-Background $g $w $h

  # the complete mark, comfortably inside the panel
  Draw-BrandMark $g ($w * 0.5) ($h * 0.34) ($w * 0.22)

  # wordmark + tagline, sized to the panel so nothing is ever clipped
  $nameFont = New-Object System.Drawing.Font("Segoe UI Semibold", [single]($w * 0.135), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $tagFont = New-Object System.Drawing.Font("Segoe UI", [single]($w * 0.048), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  $sf = New-Object System.Drawing.StringFormat
  $sf.Alignment = 'Center'
  $nb = New-Object System.Drawing.SolidBrush($Ivory)
  $tb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, $Amber))
  $rName = New-Object System.Drawing.RectangleF(0, ($h * 0.60), $w, ($h * 0.13))
  $rTag = New-Object System.Drawing.RectangleF(0, ($h * 0.695), $w, ($h * 0.08))
  $g.DrawString("KlangHub", $nameFont, $nb, $rName, $sf)
  $tagline = "LOSSLESS WHOLE-HOME AUDIO"   # plain ASCII: a separator glyph here has been mangled by the .ps1 encoding
  $g.DrawString($tagline, $tagFont, $tb, $rTag, $sf)
  $nameFont.Dispose(); $tagFont.Dispose(); $nb.Dispose(); $tb.Dispose(); $sf.Dispose()

  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $bmp.Dispose()
  Write-Output "SAVED $path ${w}x${h}"
}

function Save-Small([int]$s, [string]$path) {
  $bmp = New-Object System.Drawing.Bitmap($s, $s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $br = New-Object System.Drawing.SolidBrush($Ink)
  $g.FillRectangle($br, 0, 0, $s, $s); $br.Dispose()
  Draw-BrandMark $g ($s * 0.5) ($s * 0.5) ($s * 0.33)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $bmp.Dispose()
  Write-Output "SAVED $path ${s}x${s}"
}

Save-Large 164 314 "$out\wizard-large.bmp"
Save-Large 192 386 "$out\wizard-large-125.bmp"
Save-Large 246 471 "$out\wizard-large-150.bmp"
Save-Large 328 628 "$out\wizard-large-200.bmp"
Save-Small 55 "$out\wizard-small.bmp"
Save-Small 69 "$out\wizard-small-125.bmp"
Save-Small 83 "$out\wizard-small-150.bmp"
Save-Small 110 "$out\wizard-small-200.bmp"

