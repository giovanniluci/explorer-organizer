# Generates Assets\app.ico (16..256 px) from the official logo (Assets\logo.svg, 256x256 viewBox) with System.Drawing.
# The SVG shapes are redrawn here 1:1 (no extra tools needed). Run once with Windows PowerShell:
#   powershell -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 128, 256

function Color($hex, $alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb([int]$alpha, $c.R, $c.G, $c.B)
}

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Diagonal($x, $y, $w, $h, $from, $to) {
    # SVG linearGradient x1=0 y1=0 x2=1 y2=1 on the shape bounds
    $rect = New-Object System.Drawing.RectangleF ([float]$x), ([float]$y), ([float]$w), ([float]$h)
    return New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (Color $from), (Color $to), 45
}

$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 256.0
    $g.ScaleTransform([float]$k, [float]$k)

    # background tile
    $g.FillPath((Diagonal 8 8 240 240 '#2F8FE8' '#1565C0'), (RoundRect 8 8 240 240 56))

    # folder: back panel (55% white)
    $back = New-Object System.Drawing.Drawing2D.GraphicsPath
    $back.AddArc(48, 72, 24, 24, 180, 90)
    $back.AddLine(60, 72, 100, 72)
    $back.AddLine(100, 72, 114, 88)
    $back.AddLine(114, 88, 196, 88)
    $back.AddArc(184, 88, 24, 24, 270, 90)
    $back.AddLine(208, 100, 208, 108)
    $back.AddLine(208, 108, 48, 108)
    $back.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#FFFFFF' 140)), $back)

    # folder: front panel with a soft drop shadow (feDropShadow dy=6)
    if ($s -ge 32) {
        foreach ($i in 1..4) {
            $shadow = RoundRect (48 - $i) (98 - $i) (160 + 2 * $i) (100 + 2 * $i) (12 + $i)
            $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#0B3A7A' 22)), $shadow)
        }
    }
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#FFFFFF')), (RoundRect 48 92 160 100 12))

    # sorted lines = rules
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#1565C0')), (RoundRect 70 116 100 12 6))
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#1565C0' 166)), (RoundRect 70 140 76 12 6))
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#1565C0' 89)), (RoundRect 70 164 52 12 6))

    # "done" badge
    $g.FillEllipse((Diagonal 160 148 64 64 '#2DE0C8' '#00A6C8'), 160, 148, 64, 64)
    $ring = New-Object System.Drawing.Pen (Color '#FFFFFF'), 6
    $g.DrawEllipse($ring, 160, 148, 64, 64)
    $check = New-Object System.Drawing.Pen (Color '#FFFFFF'), 8
    $check.StartCap = 'Round'; $check.EndCap = 'Round'; $check.LineJoin = 'Round'
    $pts = [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF 177, 180), (New-Object System.Drawing.PointF 188, 191), (New-Object System.Drawing.PointF 208, 169))
    $g.DrawLines($check, $pts)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($ms.ToArray())
    if ($s -eq 256) { $bmp.Save((Join-Path $env:TEMP 'eo-logo-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}
$out = Join-Path $PSScriptRoot 'app.ico'
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
Write-Host "Wrote $out"
