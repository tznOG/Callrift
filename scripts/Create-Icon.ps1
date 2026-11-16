$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path $PSScriptRoot -Parent
$brandDirectory = Join-Path $taskRoot 'assets\brand'
$source = [Drawing.Image]::FromFile((Join-Path $brandDirectory 'callrift-logo.png'))
$frames = @()
try {
    foreach ($size in @(16,24,32,48,64,128,256)) {
        $bitmap = New-Object Drawing.Bitmap $size,$size
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $memory = New-Object IO.MemoryStream
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source,0,0,$size,$size)
            $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
            $frames += [pscustomobject]@{ Size=$size; Bytes=$memory.ToArray() }
            if ($size -eq 128) { $bitmap.Save((Join-Path $brandDirectory 'callrift-mark-128.png'),[Drawing.Imaging.ImageFormat]::Png) }
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $stream = [IO.File]::Create((Join-Path $taskRoot 'assets\callrift.ico'))
    $writer = New-Object IO.BinaryWriter $stream
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose(); $stream.Dispose() }
} finally { $source.Dispose() }
Write-Output 'Created assets\callrift.ico and assets\brand\callrift-mark-128.png.'
