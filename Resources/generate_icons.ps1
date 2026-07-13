Add-Type -AssemblyName System.Drawing

$outputDir = $PSScriptRoot
$White = [System.Drawing.Color]::White

$iconDefs = @(
    @{Name="gridwall"; Bg=[System.Drawing.Color]::FromArgb(0, 120, 215); Letter="G"}
    @{Name="beam";     Bg=[System.Drawing.Color]::FromArgb(232, 126, 4); Letter="B"}
    @{Name="floor";    Bg=[System.Drawing.Color]::FromArgb(16, 164, 74); Letter="F"}
    @{Name="mullion";  Bg=[System.Drawing.Color]::FromArgb(104, 33, 122); Letter="M"}
    @{Name="doorwin";  Bg=[System.Drawing.Color]::FromArgb(211, 55, 55); Letter="D"}
    @{Name="filter";   Bg=[System.Drawing.Color]::FromArgb(0, 139, 139); Letter="S"}
    @{Name="category"; Bg=[System.Drawing.Color]::FromArgb(139, 90, 43); Letter="C"}
    @{Name="fine";     Bg=[System.Drawing.Color]::FromArgb(0, 153, 153); Letter="Fi"}
    @{Name="about";    Bg=[System.Drawing.Color]::FromArgb(80, 80, 80); Letter="i"}
)

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
    $p = New-Object System.Drawing.Pen $White, [Math]::Max(1.5, $s/16)
    $pt = New-Object System.Drawing.Pen $White, [Math]::Max(1.0, $s/24)
    $tw = New-Object System.Drawing.SolidBrush $White
    $tb = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(100, 255, 255, 255))

    $cx = $s/2.0; $cy = $s/2.0
    $m = [int]($s * 0.2)
    $w = $s - 2*$m

    switch ($name) {
        "gridwall" {
            $g.DrawLine($p, $m, $cy, $m+$w, $cy)
            $g.DrawLine($p, $cx, $m, $cx, $m+$w)
            $g.DrawLine($pt, $m, $m, $m+$w, $m+$w)
            $g.DrawLine($pt, $m, $m+$w, $m+$w, $m)
        }
        "beam" {
            $ww = $w/3; $bh = $w*0.75
            $g.DrawLine($p, $cx-$ww, $cy-$bh/2, $cx+$ww, $cy-$bh/2)
            $g.DrawLine($p, $cx-$ww, $cy+$bh/2, $cx+$ww, $cy+$bh/2)
            $tp = New-Object System.Drawing.Pen $White, [Math]::Max(2.5, $s/10)
            $g.DrawLine($tp, $cx, $cy-$bh/2, $cx, $cy+$bh/2)
            $tp.Dispose()
        }
        "floor" {
            $r = [System.Drawing.RectangleF]::FromLTRB($m+2, $m+3, $s-$m-2, $s-$m-3)
            $g.FillRectangle($tb, $r)
            $g.DrawRectangle($p, $r.X, $r.Y, $r.Width, $r.Height)
            $g.DrawLine($pt, $m+2, $cy, $s-$m-2, $cy)
            $g.DrawLine($pt, $cx, $m+3, $cx, $s-$m-3)
        }
        "mullion" {
            $lp = New-Object System.Drawing.Pen $White, [Math]::Max(3, $s/8)
            $g.DrawLine($lp, $m, $cy, $m+$w, $cy)
            $lp.Dispose()
            $g.DrawLine($pt, $cx-$w/3, $m+3, $cx-$w/3, $s-$m-3)
            $g.DrawLine($pt, $cx+$w/3, $m+3, $cx+$w/3, $s-$m-3)
        }
        "doorwin" {
            $dw = $w; $dh = $w*0.85
            $g.DrawLine($p, $m, $m, $m, $m+$dh)
            $g.DrawLine($p, $m+$dw, $m, $m+$dw, $m+$dh)
            $g.DrawArc($p, $m, $m-1, $dw, $dw, 0, -180)
        }
        "filter" {
            $pts = New-Object System.Drawing.Drawing2D.GraphicsPath
            $pts.AddLines([System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($s*0.2, $s*0.15),
                [System.Drawing.PointF]::new($s*0.8, $s*0.15),
                [System.Drawing.PointF]::new($s*0.55, $s*0.5),
                [System.Drawing.PointF]::new($s*0.55, $s*0.85),
                [System.Drawing.PointF]::new($s*0.45, $s*0.85),
                [System.Drawing.PointF]::new($s*0.45, $s*0.5)))
            $pts.CloseFigure()
            $g.FillPath($tb, $pts)
            $g.DrawPath($p, $pts)
            $pts.Dispose()
        }
        "category" {
            $layers = @(0.08, 0.30, 0.52)
            foreach ($ly in $layers) {
                $rr = [System.Drawing.RectangleF]::FromLTRB($s*0.18, $s*$ly, $s*0.82, $s*($ly+0.24))
                $g.FillRectangle($tb, $rr)
                $g.DrawRectangle($p, $rr.X, $rr.Y, $rr.Width, $rr.Height)
            }
        }
        "fine" {
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
        }
        "about" {
            $g.DrawEllipse($p, $s*0.18, $s*0.18, $s*0.64, $s*0.64)
            $ds2 = [Math]::Max(3, $s/8)
            $g.FillEllipse($tw, $cx-$ds2/2, $s*0.28, $ds2, $ds2)
            $sw = [Math]::Max(2.5, $s/10)
            $g.FillRectangle($tw, $cx-$sw/2, $s*0.42, $sw, $s*0.32)
        }
    }
    $p.Dispose(); $pt.Dispose(); $tw.Dispose(); $tb.Dispose()
}

foreach ($def in $iconDefs) {
    $n = $def.Name
    $bg = $def.Bg
    $letter = $def.Letter
    
    foreach ($s in @(32, 16)) {
        $bmp = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $bgBrush = New-Object System.Drawing.SolidBrush $bg
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
