$lines = Get-Content 'e:\Zhare\windows-pc\HotspotShare.cs'
$outLines = @()

foreach ($line in $lines) {
    $nl = $line
    
    # Strip corner radii
    $nl = [regex]::Replace($nl, 'CornerRadius\(\d+\)', 'CornerRadius(0)')
    $nl = [regex]::Replace($nl, 'CornerRadius\(\d+,\s*\d+,\s*\d+,\s*\d+\)', 'CornerRadius(0)')
    
    # Replace blue gradients with Solid white Exact string replacements
    $nl = $nl.Replace('new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 0)', 'new SolidColorBrush(Color.FromRgb(255, 255, 255))')
    $nl = $nl.Replace('new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(6, 182, 212), 0)', 'new SolidColorBrush(Color.FromRgb(234, 51, 35))')
    $nl = $nl.Replace('new LinearGradientBrush(Color.FromRgb(129, 140, 248), Color.FromRgb(99, 102, 241), 0)', 'new SolidColorBrush(Color.FromRgb(230, 230, 230))')

    
    # Replace exact blue colors with Red
    $nl = $nl.Replace('Color.FromRgb(99, 102, 241)', 'Color.FromRgb(234, 51, 35)')
    $nl = $nl.Replace('Color.FromRgb(6, 182, 212)', 'Color.FromRgb(234, 51, 35)')
    $nl = $nl.Replace('Color.FromRgb(79, 70, 229)', 'Color.FromRgb(234, 51, 35)')
    $nl = $nl.Replace('Color.FromRgb(56, 189, 248)', 'Color.FromRgb(255, 255, 255)')

    # Dark blue backgrounds -> Pure black
    $nl = $nl.Replace('Color.FromArgb(240, 18, 30, 56)', 'Color.FromArgb(240, 15, 15, 15)')
    $nl = $nl.Replace('Color.FromArgb(200, 12, 18, 30)', 'Color.FromArgb(200, 0, 0, 0)')
    $nl = $nl.Replace('Color.FromArgb(180, 12, 18, 32)', 'Color.FromArgb(180, 0, 0, 0)')
    $nl = $nl.Replace('Color.FromArgb(160, 10, 24, 42)', 'Color.FromArgb(160, 0, 0, 0)')
    $nl = $nl.Replace('Color.FromArgb(200, 14, 22, 38)', 'Color.FromArgb(200, 0, 0, 0)')
    $nl = $nl.Replace('Color.FromRgb(19, 27, 44)', 'Color.FromRgb(0, 0, 0)')
    $nl = $nl.Replace('Color.FromRgb(30, 44, 70)', 'Color.FromRgb(51, 51, 51)')
    $nl = $nl.Replace('Color.FromRgb(30, 44, 72)', 'Color.FromRgb(51, 51, 51)')
    
    $outLines += $nl
}

Set-Content 'e:\Zhare\windows-pc\HotspotShare.cs' $outLines
Write-Output "Style fix applied"
