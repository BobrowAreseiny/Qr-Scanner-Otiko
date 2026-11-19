Add-Type -AssemblyName System.Drawing

$size = 256
$scale = $size / 456.0
$bitmap = New-Object System.Drawing.Bitmap($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#1E3A8A'))

$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, (18 * $scale))
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter

function P([float]$x, [float]$y) {
    New-Object System.Drawing.PointF(($x * $scale), ($y * $scale))
}

$graphics.DrawLines($pen, @((P 128 198), (P 128 132), (P 194 132)))
$graphics.DrawLines($pen, @((P 262 132), (P 328 132), (P 328 198)))
$graphics.DrawLines($pen, @((P 128 258), (P 128 324), (P 194 324)))
$graphics.DrawLines($pen, @((P 262 324), (P 328 324), (P 328 258)))

$bars = @(
    @(168, 180, 12, 96),
    @(192, 198, 8, 78),
    @(212, 180, 14, 96),
    @(238, 198, 8, 78),
    @(258, 180, 12, 96),
    @(282, 198, 8, 78)
)

foreach ($bar in $bars) {
    $graphics.FillRectangle(
        $white,
        [float]($bar[0] * $scale),
        [float]($bar[1] * $scale),
        [float]($bar[2] * $scale),
        [float]($bar[3] * $scale))
}

$graphics.FillRectangle(
    $white,
    [float](150 * $scale),
    [float](226 * $scale),
    [float](156 * $scale),
    [float](4 * $scale))

$pngPath = Join-Path $PSScriptRoot 'QRScannerSetup.png'
$icoPath = Join-Path $PSScriptRoot 'QRScannerSetup.ico'
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$png = [IO.File]::ReadAllBytes($pngPath)
$stream = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$png.Length)
$writer.Write([UInt32]22)
$writer.Write($png)
$writer.Flush()
[IO.File]::WriteAllBytes($icoPath, $stream.ToArray())

$writer.Dispose()
$stream.Dispose()
$pen.Dispose()
$white.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
Remove-Item $pngPath -Force

Write-Output ('icon: ' + (Get-Item $icoPath).Length + ' bytes')
