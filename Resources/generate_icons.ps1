Add-Type -AssemblyName System.Drawing

$outputDir = $PSScriptRoot
$White = [System.Drawing.Color]::White
$TransWhite = [System.Drawing.Color]::FromArgb(100, 255, 255, 255)
$FaintWhite = [System.Drawing.Color]::FromArgb(45, 255, 255, 255)

$iconDefs = @(
    @{Name="gridwall"; Bg=[System.Drawing.Color]::FromArgb(0, 120, 215)}
    @{Name="beam";     Bg=[System.Drawing.Color]::FromArgb(232, 126, 4)}
    @{Name="floor";    Bg=[System.Drawing.Color]::FromArgb(16, 164, 74)}
    @{Name="mullion";  Bg=[System.Drawing.Color]::FromArgb(104, 33, 122)}
    @{Name="doorwin";  Bg=[System.Drawing.Color]::FromArgb(211, 55, 55)}
    @{Name="filter";   Bg=[System.Drawing.Color]::FromArgb(0, 139, 139)}
    @{Name="category"; Bg=[System.Drawing.Color]::FromArgb(139, 90, 43)}
    @{Name="fine";     Bg=[System.Drawing.Color]::FromArgb(0, 153, 153)}
    @{Name="roomfloor"; Bg=[System.Drawing.Color]::FromArgb(0, 150, 136)}
    @{Name="about";    Bg=[System.Drawing.Color]::FromArgb(80, 80, 80)}
)

function New-Pen([System.Drawing.Color]$color, $width, $startCap, $endCap) {
    $p = New-Object System.Drawing.Pen($color, $width)
    if ($startCap -ne $null) { $p.StartCap = $startCap }
    if ($endCap -ne $null) { $p.EndCap = $endCap }
    return $p
}

function New-Brush([System.Drawing.Color]$color) {
    return New-Object System.Drawing.SolidBrush($color)
}

function Draw-RoundedBg {
    param($g, $x, $y, $w, $h, $r, $brush)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $r, $r, 180, 90)
    $path.AddArc($x+$w-$r, $y, $r, $r, 270, 90)
    $path.AddArc($x+$w-$r, $y+$h-$r, $r, $r, 0, 90)
    $path.AddArc($x, $y+$h-$r, $r, $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
    $path.Dispose()
}

function Draw-Symbol {
    param($g, $s, $name)
    $cx = $s/2.0; $cy = $s/2.0
    $m = [int]($s * 0.2)
    $w = $s - 2*$m

    switch ($name) {
        "gridwall" {
            $pw = [Math]::Max(1.5, $s/16)
            $pp = [Math]::Max(1.0, $s/24)
            $p = New-Pen $White $pw
            $pt = New-Pen $White $pp
            $g.DrawLine($p, $m, $cy, $m+$w, $cy)
            $g.DrawLine($p, $cx, $m, $cx, $m+$w)
            $g.DrawLine($pt, $m, $m, $m+$w, $m+$w)
            $g.DrawLine($pt, $m, $m+$w, $m+$w, $m)
            $p.Dispose(); $pt.Dispose()
        }
        "beam" {
            $ww = $w/3; $bh = $w*0.75
            $pw = [Math]::Max(1.5, $s/16)
            $tp = [Math]::Max(2.5, $s/10)
            $p = New-Pen $White $pw
            $g.DrawLine($p, $cx-$ww, $cy-$bh/2, $cx+$ww, $cy-$bh/2)
            $g.DrawLine($p, $cx-$ww, $cy+$bh/2, $cx+$ww, $cy+$bh/2)
            $p.Dispose()
            $tp2 = New-Pen $White $tp
            $g.DrawLine($tp2, $cx, $cy-$bh/2, $cx, $cy+$bh/2)
            $tp2.Dispose()
        }
        "floor" {
            $pw = [Math]::Max(1.5, $s/16)
            $pp = [Math]::Max(1.0, $s/24)
            $p = New-Pen $White $pw
            $pt = New-Pen $White $pp
            $r = [System.Drawing.RectangleF]::FromLTRB($m+2, $m+3, $s-$m-2, $s-$m-3)
            $tb = New-Brush $TransWhite
            $g.FillRectangle($tb, $r)
            $tb.Dispose()
            $g.DrawRectangle($p, $r.X, $r.Y, $r.Width, $r.Height)
            $g.DrawLine($pt, $m+2, $cy, $s-$m-2, $cy)
            $g.DrawLine($pt, $cx, $m+3, $cx, $s-$m-3)
            $p.Dispose(); $pt.Dispose()
        }
        "roomfloor" {
            $wallW = [Math]::Max(3.5, $s/6.5)
            $m2 = [int]($s * 0.17)
            $door = [int]($s * 0.28)
            $wp = New-Pen $White $wallW ([System.Drawing.Drawing2D.LineCap]::Round) ([System.Drawing.Drawing2D.LineCap]::Round)
            $g.DrawLine($wp, $m2, $m2, $s-$m2, $m2)
            $g.DrawLine($wp, $m2, $m2, $m2, $s-$m2)
            $g.DrawLine($wp, $s-$m2, $m2, $s-$m2, $s-$m2)
            $g.DrawLine($wp, $m2, $s-$m2, $m2+$door, $s-$m2)
            $g.DrawLine($wp, $s-$m2, $s-$m2, $m2+$door, $s-$m2)
            $wp.Dispose()

            $inset = $m2 + $wallW
            $fr = [System.Drawing.RectangleF]::FromLTRB($inset, $inset, $s-$inset, $s-$inset)
            $fb = New-Brush $FaintWhite
            $g.FillRectangle($fb, $fr)
            $fb.Dispose()

            $fpw = [Math]::Max(0.8, $s/32)
            $fp = New-Pen ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)) $fpw
            $g.DrawRectangle($fp, $fr.X, $fr.Y, $fr.Width, $fr.Height)
            $g.DrawLine($fp, $inset, $cy, $s-$inset, $cy)
            $g.DrawLine($fp, $cx, $inset, $cx, $s-$inset)
            $fp.Dispose()

            $dpw = [Math]::Max(1.2, $s/22)
            $dp = New-Pen $White $dpw
            $g.DrawArc($dp, $m2, $s-$m2-$door, $door, $door, 0, 90)
            $dp.Dispose()
        }
        "mullion" {
            $lpw = [Math]::Max(3, $s/8)
            $pp = [Math]::Max(1.0, $s/24)
            $lp = New-Pen $White $lpw
            $g.DrawLine($lp, $m, $cy, $m+$w, $cy)
            $lp.Dispose()
            $pt = New-Pen $White $pp
            $g.DrawLine($pt, $cx-$w/3, $m+3, $cx-$w/3, $s-$m-3)
            $g.DrawLine($pt, $cx+$w/3, $m+3, $cx+$w/3, $s-$m-3)
            $pt.Dispose()
        }
        "doorwin" {
            $pw = [Math]::Max(1.5, $s/16)
            $p = New-Pen $White $pw
            $dw = $w; $dh = $w*0.85
            $g.DrawLine($p, $m, $m, $m, $m+$dh)
            $g.DrawLine($p, $m+$dw, $m, $m+$dw, $m+$dh)
            $g.DrawArc($p, $m, $m-1, $dw, $dw, 0, -180)
            $p.Dispose()
        }
        "filter" {
            $pw = [Math]::Max(1.5, $s/16)
            $p = New-Pen $White $pw
            $pts = New-Object System.Drawing.Drawing2D.GraphicsPath
            $pts.AddLines([System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($s*0.2, $s*0.15),
                [System.Drawing.PointF]::new($s*0.8, $s*0.15),
                [System.Drawing.PointF]::new($s*0.55, $s*0.5),
                [System.Drawing.PointF]::new($s*0.55, $s*0.85),
                [System.Drawing.PointF]::new($s*0.45, $s*0.85),
                [System.Drawing.PointF]::new($s*0.45, $s*0.5)))
            $pts.CloseFigure()
            $tb = New-Brush $TransWhite
            $g.FillPath($tb, $pts)
            $tb.Dispose()
            $g.DrawPath($p, $pts)
            $pts.Dispose()
            $p.Dispose()
        }
        "category" {
            $pw = [Math]::Max(1.5, $s/16)
            $p = New-Pen $White $pw
            $layers = @(0.08, 0.30, 0.52)
            foreach ($ly in $layers) {
                $rr = [System.Drawing.RectangleF]::FromLTRB($s*0.18, $s*$ly, $s*0.82, $s*($ly+0.24))
                $tb = New-Brush $TransWhite
                $g.FillRectangle($tb, $rr)
                $tb.Dispose()
                $g.DrawRectangle($p, $rr.X, $rr.Y, $rr.Width, $rr.Height)
            }
            $p.Dispose()
        }
        "fine" {
            $pw = [Math]::Max(1.5, $s/16)
            $pp = [Math]::Max(1.0, $s/24)
            $p = New-Pen $White $pw
            $pt = New-Pen $White $pp
            $tw = New-Brush $White
            $pts2 = New-Object System.Drawing.Drawing2D.GraphicsPath
            $pts2.AddLines([System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($s*0.3, $s*0.15),
                [System.Drawing.PointF]::new($s*0.7, $s*0.15),
                [System.Drawing.PointF]::new($s*0.52, $s*0.5),
                [System.Drawing.PointF]::new($s*0.52, $s*0.65),
                [System.Drawing.PointF]::new($s*0.48, $s*0.65),
                [System.Drawing.PointF]::new($s*0.48, $s*0.5)))
            $g.DrawPath($p, $pts2)
            $pts2.Dispose()
            $g.DrawLine($pt, $s*0.48, $s*0.5, $s*0.52, $s*0.5)
            $ds = [Math]::Max(1.5, $s/12)
            $g.FillEllipse($tw, $s*0.38, $s*0.72, $ds, $ds)
            $g.FillEllipse($tw, $s*0.50, $s*0.76, $ds, $ds)
            $g.FillEllipse($tw, $s*0.60, $s*0.70, $ds, $ds)
            $p.Dispose(); $pt.Dispose(); $tw.Dispose()
        }
        "about" {
            $pw = [Math]::Max(1.5, $s/16)
            $p = New-Pen $White $pw
            $tw = New-Brush $White
            $g.DrawEllipse($p, $s*0.18, $s*0.18, $s*0.64, $s*0.64)
            $ds2 = [Math]::Max(3, $s/8)
            $g.FillEllipse($tw, $cx-$ds2/2, $s*0.28, $ds2, $ds2)
            $sw = [Math]::Max(2.5, $s/10)
            $g.FillRectangle($tw, $cx-$sw/2, $s*0.42, $sw, $s*0.32)
            $p.Dispose(); $tw.Dispose()
        }
    }
}

foreach ($def in $iconDefs) {
    $n = $def.Name
    $bg = $def.Bg
    
    foreach ($s in @(32, 16)) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $bgBrush = New-Brush $bg
        Draw-RoundedBg $g 1 1 ($s-2) ($s-2) ([Math]::Floor($s/4)) $bgBrush
        $bgBrush.Dispose()

        Draw-Symbol $g $s $n

        $g.Dispose()
        $bmp.Save([System.IO.Path]::Combine($outputDir, "icon_${n}_${s}.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Created icon_${n}_${s}.png"
    }
}

Write-Host "`nAll icons generated successfully!"
