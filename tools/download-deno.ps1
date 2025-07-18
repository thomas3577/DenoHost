param (
    [string]$ExecutablePath,
    [string]$DownloadFilename,
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

$downloadUrl = "https://github.com/denoland/deno/releases/download/v$($denoVersion)/$($DownloadFilename)"
# Use unique temp file name to avoid conflicts when multiple projects build in parallel
$tempZip = "$env:TEMP\deno-$([System.Guid]::NewGuid().ToString('N').Substring(0,8)).zip"
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

# Clean up temp file with retry logic
try {
    Remove-Item $tempZip -ErrorAction Stop
} catch {
    Write-Warning "Could not remove temp file $tempZip : $($_.Exception.Message)"
    # Try to remove it again after a short delay
    Start-Sleep -Milliseconds 100
    try {
        Remove-Item $tempZip -ErrorAction Stop
    } catch {
        Write-Warning "Second attempt to remove temp file failed. Continuing anyway."
    }
}
Write-Host "Deno setup complete at $ExecutablePath"
