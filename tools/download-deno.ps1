param (
    [string]$ExecutablePath,
    [string]$DownloadUrl
)

if (Test-Path $ExecutablePath) {
    Write-Host "Deno already exists at $ExecutablePath"
    return
}

$tempZip = "$env:TEMP\deno.zip"
$extractDir = Split-Path $ExecutablePath

Write-Host "Downloading Deno from $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempZip -UseBasicParsing

Write-Host "Extracting..."
Expand-Archive -Path $tempZip -DestinationPath $extractDir -Force

# Rename, falls nötig
$downloadedExe = Get-ChildItem -Path $extractDir -Recurse | Where-Object { $_.Name -like "deno*" -and !$_.PSIsContainer } | Select-Object -First 1
if ($downloadedExe -and $downloadedExe.FullName -ne $ExecutablePath) {
    Rename-Item -Path $downloadedExe.FullName -NewName (Split-Path $ExecutablePath -Leaf)
}

Remove-Item $tempZip
Write-Host "Deno setup complete at $ExecutablePath"
