Add-Type -AssemblyName System.Drawing
$icon = New-Object System.Drawing.Icon("e:\Zhare\zhare_icon.ico")
$bitmap = $icon.ToBitmap()
$folders = @("mipmap-hdpi", "mipmap-mdpi", "mipmap-xhdpi", "mipmap-xxhdpi", "mipmap-xxxhdpi")
foreach ($folder in $folders) {
    if (-not (Test-Path "e:\Zhare\app\src\main\res\$folder")) {
        New-Item -ItemType Directory -Path "e:\Zhare\app\src\main\res\$folder" -Force | Out-Null
    }
    $bitmap.Save("e:\Zhare\app\src\main\res\$folder\ic_launcher.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Save("e:\Zhare\app\src\main\res\$folder\ic_launcher_round.png", [System.Drawing.Imaging.ImageFormat]::Png)
}
Write-Output "Done"
