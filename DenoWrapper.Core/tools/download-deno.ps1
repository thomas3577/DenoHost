param(
  [string]$Version = "1.45.0"
)

$baseUrl = "https://github.com/denoland/deno/releases/download/v$Version"
$arch = "x86_64-pc-windows-msvc"
$zipUrl = "$baseUrl/deno-$arch.zip"
$destDir = "runtimes/win-x64/native"

Write-Host "Downloading Deno $Version..."
Invoke-WebRequest -Uri $zipUrl -OutFile "deno.zip"
Expand-Archive -Path "deno.zip" -DestinationPath $destDir
Remove-Item "deno.zip"
