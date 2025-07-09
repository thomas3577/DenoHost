param (
    [string]$ExecutablePath,
    [string]$DownloadFileName
    [string]$DevDenoVersion
)

if (Test-Path $ExecutablePath) {
    Write-Host "Deno already exists at $ExecutablePath"
    return
}

# Get last Git-Tag (e.g. "v2.4.1-alpha.1")
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $repoRoot
$gitTag = git describe --tags --abbrev=0 2>$null
Pop-Location

if (-not $gitTag) {
    Write-Error "Could not determine Git tag. Using fallback."
    $gitTag = "v$DevDenoVersion" # Fallback
}

# Extract only the main version (e.g. 2.4.1 from v2.4.1-alpha.1)
if ($gitTag -match '^v?(\d+\.\d+\.\d+)') {
    $denoVersion = $matches[1]
} else {
    Write-Error "Could not parse Deno version from Git tag: $gitTag"
    exit 1
}

$downloadUrl = "https://github.com/denoland/deno/releases/download/v$($denoVersion)/$($DownloadFileName)"
$tempZip = "$env:TEMP\deno.zip"
$extractDir = Split-Path $ExecutablePath

Write-Host "Downloading Deno from $downloadUrl"
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing

Write-Host "Extracting to $extractDir"
Expand-Archive -Path $tempZip -DestinationPath $extractDir -Force

# Rename if necessary
$downloadedExe = Get-ChildItem -Path $extractDir -Recurse | Where-Object { $_.Name -like "deno*" -and !$_.PSIsContainer } | Select-Object -First 1
if ($downloadedExe -and $downloadedExe.FullName -ne $ExecutablePath) {
    Rename-Item -Path $downloadedExe.FullName -NewName (Split-Path $ExecutablePath -Leaf)
}

Remove-Item $tempZip
Write-Host "Deno setup complete at $ExecutablePath"
