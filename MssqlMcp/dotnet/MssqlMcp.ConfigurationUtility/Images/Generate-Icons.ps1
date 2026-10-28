<#
.SYNOPSIS
    Generates the configuration utility's icons: a database cylinder with a badge showing the window's purpose.

.DESCRIPTION
    Each .ico contains 16, 20, 24, 32, 40, 48, 64 (32-bit bitmaps) and 256 (PNG) pixel images, each drawn at its
    own size so small sizes stay sharp. Badges are omitted below 24 pixels, where they would be unreadable.
    Run with Windows PowerShell 5.1:  powershell.exe -ExecutionPolicy Bypass -File Generate-Icons.ps1
#>
param(
    [string] $OutputDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function New-Color([string] $hex) {
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

# Draws the database cylinder on a 256x256 design grid
function Draw-Database([System.Drawing.Graphics] $g) {
    $left = 36; $width = 160; $top = 32; $ellipseHeight = 48; $bottom = 216

    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddArc($left, $top, $width, $ellipseHeight, 180, 180)
    $body.AddLine($left + $width, $top + $ellipseHeight / 2, $left + $width, $bottom)
    $body.AddArc($left, $bottom - $ellipseHeight / 2, $width, $ellipseHeight, 0, 180)
    $body.CloseFigure()

    $bodyBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Point($left, 0)), (New-Object System.Drawing.Point(($left + $width), 0)), (New-Color '#1F5FA8'), (New-Color '#1A4F8F'))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
    $blend.Colors = @((New-Color '#1F5FA8'), (New-Color '#4A97E3'), (New-Color '#17467F'))
    $blend.Positions = @(0.0, 0.35, 1.0)
    $bodyBrush.InterpolationColors = $blend
    $g.FillPath($bodyBrush, $body)

    # Lines between the "discs"
    $bandPen = New-Object System.Drawing.Pen((New-Color '#A9D0F5'), 7)
    foreach ($offset in 61, 122) {
        $g.DrawArc($bandPen, $left + 4, $top + $offset, $width - 8, $ellipseHeight, 15, 150)
    }

    $topBrush = New-Object System.Drawing.SolidBrush((New-Color '#8EC3F3'))
    $g.FillEllipse($topBrush, $left, $top, $width, $ellipseHeight)

    $outline = New-Object System.Drawing.Pen((New-Color '#143F73'), 8)
    $g.DrawPath($outline, $body)
    $g.DrawEllipse($outline, $left, $top, $width, $ellipseHeight)

    $bodyBrush.Dispose(); $bandPen.Dispose(); $topBrush.Dispose(); $outline.Dispose(); $body.Dispose()
}

# Draws the circular badge background and returns its centre
function Draw-BadgeBackground([System.Drawing.Graphics] $g, [string] $colour) {
    $cx = 188; $cy = 186; $r = 64

    $fill = New-Object System.Drawing.SolidBrush((New-Color $colour))
    $ring = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 10)
    $g.FillEllipse($fill, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $g.DrawEllipse($ring, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $fill.Dispose(); $ring.Dispose()

    return @($cx, $cy)
}

function Draw-GearBadge([System.Drawing.Graphics] $g) {
    $colour = '#E8890C'
    $centre = Draw-BadgeBackground $g $colour
    $cx = $centre[0]; $cy = $centre[1]
    $white = [System.Drawing.Brushes]::White

    $state = $g.Save()
    $g.TranslateTransform($cx, $cy)
    for ($i = 0; $i -lt 8; $i++) {
        $g.FillRectangle($white, -9, -44, 18, 20)
        $g.RotateTransform(45)
    }
    $g.Restore($state)

    $g.FillEllipse($white, $cx - 30, $cy - 30, 60, 60)
    $hole = New-Object System.Drawing.SolidBrush((New-Color $colour))
    $g.FillEllipse($hole, $cx - 12, $cy - 12, 24, 24)
    $hole.Dispose()
}

function Draw-PencilBadge([System.Drawing.Graphics] $g) {
    $centre = Draw-BadgeBackground $g '#2E9E4F'
    $white = [System.Drawing.Brushes]::White

    $state = $g.Save()
    $g.TranslateTransform($centre[0], $centre[1])
    $g.RotateTransform(45)
    $g.FillRectangle($white, -11, -40, 22, 56)
    $tip = [System.Drawing.Point[]] @((New-Object System.Drawing.Point(-11, 20)), (New-Object System.Drawing.Point(11, 20)), (New-Object System.Drawing.Point(0, 42)))
    $g.FillPolygon($white, $tip)
    $g.Restore($state)
}

function Draw-PlugBadge([System.Drawing.Graphics] $g) {
    $centre = Draw-BadgeBackground $g '#7B4FC9'
    $cx = $centre[0]; $cy = $centre[1]
    $white = [System.Drawing.Brushes]::White

    $g.FillRectangle($white, $cx - 13, $cy - 40, 8, 22)
    $g.FillRectangle($white, $cx + 5, $cy - 40, 8, 22)
    $g.FillRectangle($white, $cx - 24, $cy - 20, 48, 30)
    $cable = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 10)
    $g.DrawLine($cable, $cx, $cy + 8, $cx, $cy + 40)
    $cable.Dispose()
}

function New-IconImage([int] $size, [scriptblock] $drawBadge) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    Draw-Database $g

    if ($size -ge 24 -and $drawBadge -ne $null) {
        & $drawBadge $g
    }

    $g.Dispose()

    return $bitmap
}

# Encodes a bitmap as an ICO image entry: PNG for 256 pixels, otherwise a 32-bit DIB with an empty AND mask
function Get-IconImageBytes([System.Drawing.Bitmap] $bitmap) {
    $size = $bitmap.Width
    $stream = New-Object System.IO.MemoryStream

    if ($size -ge 256) {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }

    $writer = New-Object System.IO.BinaryWriter($stream)
    $maskRowBytes = [int] ([Math]::Ceiling($size / 32.0) * 4)

    # BITMAPINFOHEADER (height is doubled to include the AND mask)
    $writer.Write([int] 40); $writer.Write([int] $size); $writer.Write([int] ($size * 2))
    $writer.Write([int16] 1); $writer.Write([int16] 32); $writer.Write([int] 0)
    $writer.Write([int] ($size * $size * 4 + $maskRowBytes * $size))
    $writer.Write([int] 0); $writer.Write([int] 0); $writer.Write([int] 0); $writer.Write([int] 0)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bitmap.UnlockBits($data)

    # Rows are stored bottom-up
    for ($y = $size - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $data.Stride, $size * 4)
    }

    $writer.Write((New-Object byte[] ($maskRowBytes * $size)))
    $writer.Flush()

    return $stream.ToArray()
}

function Save-Icon([string] $fileName, [scriptblock] $drawBadge) {
    $images = foreach ($size in $sizes) {
        $bitmap = New-IconImage $size $drawBadge
        $bytes = Get-IconImageBytes $bitmap
        $bitmap.Dispose()
        , @($size, $bytes)
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    # ICONDIR
    $writer.Write([int16] 0); $writer.Write([int16] 1); $writer.Write([int16] $images.Count)

    # ICONDIRENTRY for each image
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image[0] -ge 256) { 0 } else { $image[0] }
        $writer.Write([byte] $dimension); $writer.Write([byte] $dimension); $writer.Write([byte] 0); $writer.Write([byte] 0)
        $writer.Write([int16] 1); $writer.Write([int16] 32)
        $writer.Write([int] $image[1].Length); $writer.Write([int] $offset)
        $offset += $image[1].Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]] $image[1])
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory $fileName), $stream.ToArray())
    "Saved $fileName"
}

Save-Icon 'MssqlMcpConfiguration.ico' ${function:Draw-GearBadge}
Save-Icon 'DatabaseEdit.ico' ${function:Draw-PencilBadge}
Save-Icon 'ConnectionStringBuilder.ico' ${function:Draw-PlugBadge}
