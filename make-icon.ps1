# Genera app.ico (16-256 px, immagini PNG) per Desktop per Desktop
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot 'app.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $k = $s / 256.0

    # Due "schermi" dietro (gli altri desktop)
    $back2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 74, 140))
    $back1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 99, 177))
    $front = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 212))
    $g.FillPath($back2, (RoundRect (56*$k) (8*$k) (192*$k) (150*$k) (22*$k)))
    $g.FillPath($back1, (RoundRect (32*$k) (36*$k) (192*$k) (150*$k) (22*$k)))
    # Schermo davanti
    $g.FillPath($front, (RoundRect (8*$k) (66*$k) (192*$k) (150*$k) (22*$k)))

    # Cartella gialla sullo schermo davanti
    $tab = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 170, 30))
    $body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 200, 60))
    $g.FillPath($tab, (RoundRect (48*$k) (100*$k) (56*$k) (30*$k) (8*$k)))
    $g.FillPath($body, (RoundRect (48*$k) (114*$k) (112*$k) (76*$k) (10*$k)))

    # Piede del monitor
    $stand = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 74, 140))
    $g.FillPath($stand, (RoundRect (74*$k) (222*$k) (60*$k) (26*$k) (8*$k)))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
"Creato $out"
