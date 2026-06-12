Add-Type -AssemblyName System.Drawing
$dir = 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Art\Generated\BodyFrames'
$pieces = @('Z','O','L','J','S','T','I')
$names = @{ Z='Aggraso'; O='Muwerde'; L='Galiffambos'; J='Dousema'; S='Sorrisol'; T='Lysergicada'; I='Blyndoolie' }
$cell = 240
$width = (3 * $cell) + 160
$height = (7 * $cell) + 40
$sheet = New-Object System.Drawing.Bitmap -ArgumentList $width, $height
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(34, 36, 48))
$font = New-Object System.Drawing.Font -ArgumentList 'Segoe UI', 13
$brush = [System.Drawing.Brushes]::White
for ($r = 0; $r -lt 7; $r++) {
    $p = $pieces[$r]
    $label = '{0} / {1}' -f $p, $names[$p]
    $labelY = ($r * $cell) + 20 + ($cell / 2) - 10
    $g.DrawString($label, $font, $brush, 4, $labelY)
    for ($f = 1; $f -le 3; $f++) {
        $path = Join-Path $dir ('{0}_frame{1}.png' -f $p, $f)
        $img = [System.Drawing.Image]::FromFile($path)
        $scale = [Math]::Min(($cell - 16) / $img.Width, ($cell - 16) / $img.Height)
        $w = [int]($img.Width * $scale)
        $h = [int]($img.Height * $scale)
        $x = [int](160 + (($f - 1) * $cell) + (($cell - $w) / 2))
        $y = [int](($r * $cell) + 20 + (($cell - $h) / 2))
        $g.DrawImage($img, $x, $y, $w, $h)
        $img.Dispose()
    }
}
$g.Dispose()
$sheet.Save('C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\animation-contact-sheet.png')
$sheet.Dispose()
Write-Output 'contact sheet saved'
