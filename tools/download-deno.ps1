param (
  [string]$ExecutablePath,
  [string]$DownloadFilename,
  [string]$DenoVersion
)

# Check if Deno exists and version matches
$needsDownload = $true
if (Test-Path $ExecutablePath) {
  Write-Host "Deno binary found at $ExecutablePath, checking version..."

  try {
    # Try to get version from file properties first (fastest)
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath)
    if ($versionInfo.ProductVersion -and $versionInfo.ProductVersion -match '(\d+\.\d+\.\d+)') {
      $currentVersion = $matches[1]
      Write-Host "File version: $currentVersion, Required: $DenoVersion"

      if ($currentVersion -eq $DenoVersion) {
        Write-Host "Version matches! No download needed."
        $needsDownload = $false
      } else {
        Write-Host "Version mismatch! Will download correct version."
      }
    } else {
      Write-Warning "Could not determine version from file info. Will re-download."
    }
  } catch {
    Write-Warning "Error checking Deno version: $($_.Exception.Message). Will re-download."
  }
}

if (-not $needsDownload) {
  Write-Host "Deno setup complete at $ExecutablePath"
  exit 0
}

$downloadUrl = "https://github.com/denoland/deno/releases/download/v$($DenoVersion)/$($DownloadFilename)"
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
