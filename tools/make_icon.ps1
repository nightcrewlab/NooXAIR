# NooXAIR ikon ve kurulum gorselleri uretici (GDI+)
# Cikti: assets\nooxair.ico, assets\nooxair_256.png, assets\wizard.bmp, assets\wizard_small.bmp
param([string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "assets"))

Add-Type -AssemblyName System.Drawing
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$C_BG1   = [System.Drawing.Color]::FromArgb(255, 22, 29, 50)
$C_BG2   = [System.Drawing.Color]::FromArgb(255, 7, 10, 18)
$C_CYAN  = [System.Drawing.Color]::FromArgb(255, 34, 211, 238)
$C_VIOL  = [System.Drawing.Color]::FromArgb(255, 139, 92, 246)

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Glyph: ekran + yayin dalgalari. $ox,$oy,$s = cizim alani
function Draw-Glyph($g, [single]$ox, [single]$oy, [single]$s, [bool]$small) {
    $stroke = [Math]::Max(1.6, $s * ($(if ($small) { 0.085 } else { 0.058 })))
    $rect = New-Object System.Drawing.RectangleF($ox, $oy, $s, $s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $C_CYAN, $C_VIOL, [single]35)
    $pen = New-Object System.Drawing.Pen($grad, $stroke)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    # Ekran
    $sx = $ox + $s * 0.20; $sy = $oy + $s * 0.20; $sw = $s * 0.60; $sh = $s * 0.38
    $screen = New-RoundRect $sx $sy $sw $sh ($s * 0.07)
    $g.DrawPath($pen, $screen)

    # Yayin noktasi + dalgalar (ekranin altindan yukari bakan)
    $cx = $ox + $s * 0.50; $cy = $oy + $s * 0.83
    $dot = $s * $(if ($small) { 0.075 } else { 0.055 })
    $g.FillEllipse($grad, $cx - $dot, $cy - $dot, $dot * 2, $dot * 2)
    $radii = if ($small) { @(0.17) } else { @(0.13, 0.22) }
    foreach ($rr in $radii) {
        $r = $s * $rr
        $g.DrawArc($pen, $cx - $r, $cy - $r, $r * 2, $r * 2, 228, 84)
    }
    $pen.Dispose(); $grad.Dispose(); $screen.Dispose()
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [single]$size
    $pad = [Math]::Max(0.5, $s * 0.03)
    $tile = New-RoundRect $pad $pad ($s - 2 * $pad) ($s - 2 * $pad) ($s * 0.22)
    $bgRect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bgRect, $C_BG1, $C_BG2, [single]60)
    $g.FillPath($bg, $tile)
    # ince kenar parlamasi
    $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(46, 255, 255, 255), [Math]::Max(1, $s / 128))
    $g.DrawPath($edge, $tile)
    # ust tarafta hafif isik
    if ($size -ge 48) {
        $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glowPath.AddEllipse($s * 0.05, -$s * 0.45, $s * 0.9, $s * 0.8)
        $glow = New-Object System.Drawing.Drawing2D.PathGradientBrush($glowPath)
        $glow.CenterColor = [System.Drawing.Color]::FromArgb(40, 34, 211, 238)
        $glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 34, 211, 238))
        $g.SetClip($tile)
        $g.FillPath($glow, $glowPath)
        $g.ResetClip()
        $glow.Dispose(); $glowPath.Dispose()
    }
    Draw-Glyph $g 0 0 $s ($size -le 24)
    $g.Dispose(); $bg.Dispose(); $edge.Dispose(); $tile.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b = $ms.ToArray(); $ms.Dispose(); return ,$b
}

# --- ICO (PNG gomulu, Vista+ formati) ---
$sizes = @(256, 128, 64, 48, 40, 32, 24, 20, 16)
$images = @()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    if ($sz -eq 256) { $bmp.Save((Join-Path $OutDir "nooxair_256.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $images += ,(Get-PngBytes $bmp)
    $bmp.Dispose()
}
$icoPath = Join-Path $OutDir "nooxair.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $len = $images[$i].Length
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close(); $fs.Close()

# --- Inno Setup sihirbaz gorselleri (dark) ---
function Save-Bmp24($bmp, $path) {
    $out = New-Object System.Drawing.Bitmap($bmp.Width, $bmp.Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($out); $g.DrawImage($bmp, 0, 0, $bmp.Width, $bmp.Height); $g.Dispose()
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp); $out.Dispose()
}

$W = 328; $H = 628   # 164x314 @200% (Inno otomatik olcekler)
$big = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($big)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$bgb = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0, 0, $W, $H)), $C_BG1, $C_BG2, [single]90)
$g.FillRectangle($bgb, 0, 0, $W, $H)
$ic = New-IconBitmap 176
$g.DrawImage($ic, [int](($W - 176) / 2), 150, 176, 176)
$f1 = New-Object System.Drawing.Font("Segoe UI Semibold", 30)
$f2 = New-Object System.Drawing.Font("Segoe UI", 13)
$sf = New-Object System.Drawing.StringFormat; $sf.Alignment = [System.Drawing.StringAlignment]::Center
$tb = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(60, 360, 210, 60)), $C_CYAN, $C_VIOL, [single]0)
$g.DrawString("NooXAIR", $f1, $tb, (New-Object System.Drawing.RectangleF(0, 360, $W, 60)), $sf)
$mb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 138, 148, 168))
$g.DrawString("AirPlay Receiver", $f2, $mb, (New-Object System.Drawing.RectangleF(0, 420, $W, 30)), $sf)
$g.Dispose()
Save-Bmp24 $big (Join-Path $OutDir "wizard.bmp")

$sm = New-Object System.Drawing.Bitmap(110, 110)
$g = [System.Drawing.Graphics]::FromImage($sm)
$g.Clear([System.Drawing.Color]::White)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$ic2 = New-IconBitmap 110
$g.DrawImage($ic2, 0, 0, 110, 110); $g.Dispose()
Save-Bmp24 $sm (Join-Path $OutDir "wizard_small.bmp")

Write-Host "[OK] Ikon ve gorseller uretildi: $OutDir" -ForegroundColor Green
