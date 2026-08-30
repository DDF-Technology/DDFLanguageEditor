param(
    [string]$Source = (Join-Path $PSScriptRoot '..\Resource\AppIcon.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\Resource\DDFLanguageEditor.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath([System.Drawing.RectangleF]$rectangle, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($rectangle.X, $rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rectangle.X, $rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-SmallIconBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $inset = [Math]::Max(1, [Math]::Round($size * 0.04))
    $edge = $size - (2 * $inset) - 1
    $rect = [System.Drawing.RectangleF]::new([single]$inset, [single]$inset, [single]$edge, [single]$edge)
    $path = New-RoundedRectanglePath $rect ([Math]::Max(2, $size * 0.18))
    $graphics.FillPath([System.Drawing.Brushes]::White, $path)
    $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(185, 12, 36, 62), [Math]::Max(1, $size * 0.045))
    $graphics.DrawPath($border, $path)

    $navy = [System.Drawing.Color]::FromArgb(8, 34, 59)
    $orange = [System.Drawing.Color]::FromArgb(255, 176, 0)
    $stroke = [Math]::Max(2, $size * 0.12)
    $left = New-Object System.Drawing.Pen($navy, $stroke)
    $right = New-Object System.Drawing.Pen($orange, $stroke)
    $left.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $left.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $right.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $right.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $mid = $size * 0.5
    $top = $size * 0.29
    $bottom = $size * 0.71
    $graphics.DrawLines($left, [System.Drawing.PointF[]]@(
        ([System.Drawing.PointF]::new([single]($size * 0.39), [single]$top)),
        ([System.Drawing.PointF]::new([single]($size * 0.22), [single]$mid)),
        ([System.Drawing.PointF]::new([single]($size * 0.39), [single]$bottom))))
    $graphics.DrawLines($right, [System.Drawing.PointF[]]@(
        ([System.Drawing.PointF]::new([single]($size * 0.57), [single]$top)),
        ([System.Drawing.PointF]::new([single]($size * 0.75), [single]$mid)),
        ([System.Drawing.PointF]::new([single]($size * 0.57), [single]$bottom))))

    $left.Dispose()
    $right.Dispose()
    $border.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    return $bitmap
}

function New-LargeIconBitmap([System.Drawing.Image]$image, [int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.DrawImage($image, 0, 0, $size, $size)
    $graphics.Dispose()
    return $bitmap
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path $Source).Path)
$frames = New-Object System.Collections.Generic.List[byte[]]
try {
    foreach ($size in $sizes) {
        $bitmap = if ($size -le 48) { New-SmallIconBitmap $size } else { New-LargeIconBitmap $sourceImage $size }
        try {
            $stream = New-Object System.IO.MemoryStream
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
            $stream.Dispose()
        }
        finally { $bitmap.Dispose() }
    }
}
finally { $sourceImage.Dispose() }

$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$file = [System.IO.File]::Open($destinationPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output $destinationPath
