param (
  [string]$ExecutablePath,
  [string]$DownloadFilename,
  [string]$DenoVersion,
  [string]$RuntimeRid = ""
)

$ErrorActionPreference = 'Stop'

function Get-ExpectedSha256 {
  param (
    [string]$ChecksumFile,
    [string]$TargetFileName
  )

  $content = Get-Content -Path $ChecksumFile -Raw
  $escapedFileName = [Regex]::Escape($TargetFileName)
  $lineMatch = [Regex]::Match($content, "(?im)^\s*([a-f0-9]{64})\s+\*?" + $escapedFileName + "\s*$")
  if ($lineMatch.Success) {
    return $lineMatch.Groups[1].Value.ToLowerInvariant()
  }

  $firstHashMatch = [Regex]::Match($content, "(?im)^\s*([a-f0-9]{64})\b")
  if ($firstHashMatch.Success) {
    return $firstHashMatch.Groups[1].Value.ToLowerInvariant()
  }

  throw "Unable to extract SHA-256 for '$TargetFileName' from checksum file '$ChecksumFile'."
}

function Write-ExecutableChecksum {
  param (
    [string]$Executable,
    [string]$OutputFile
  )

  if (-not (Test-Path $Executable)) {
    throw "Executable not found at '$Executable'."
  }

  $hash = (Get-FileHash -Algorithm SHA256 -Path $Executable).Hash.ToLowerInvariant()
  $exeName = Split-Path $Executable -Leaf
  Set-Content -Path $OutputFile -Value "$hash  $exeName"
  Write-Host "Wrote executable checksum to $OutputFile"
}

function Resolve-RuntimeRid {
  param (
    [string]$Executable,
    [string]$ProvidedRuntimeRid
  )

  if (-not [string]::IsNullOrWhiteSpace($ProvidedRuntimeRid)) {
    return $ProvidedRuntimeRid
  }

  $runtimeDir = Split-Path (Split-Path $Executable -Parent) -Leaf
  if ($runtimeDir -like "DenoHost.Runtime.*") {
    return $runtimeDir.Substring("DenoHost.Runtime.".Length)
  }

  return "unknown"
}

function Write-RuntimeMetadata {
  param (
    [string]$Executable,
    [string]$Version,
    [string]$Rid,
    [string]$ArchiveName
  )

  $exeName = Split-Path $Executable -Leaf
  $exeHash = (Get-FileHash -Algorithm SHA256 -Path $Executable).Hash.ToLowerInvariant()
  $metadataPath = Join-Path (Split-Path $Executable -Parent) "deno.metadata.json"
  $metadata = [ordered]@{
    metadataVersion = 1
    fileName = $exeName
    rid = $Rid
    denoVersion = $Version
    sha256 = $exeHash
    source = "https://github.com/denoland/deno/releases/download/v$Version/$ArchiveName"
    createdAtUtc = [DateTime]::UtcNow.ToString("o")
  }

  $metadataJson = $metadata | ConvertTo-Json -Compress
  [System.IO.File]::WriteAllText($metadataPath, $metadataJson, [System.Text.UTF8Encoding]::new($false))
  Write-Host "Wrote runtime metadata to $metadataPath"
  return $metadataPath
}

function Sign-RuntimeMetadata {
  param (
    [string]$MetadataPath
  )

  $privateKeyPem = [Environment]::GetEnvironmentVariable("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM")
  $requireSignature = [string]::Equals([Environment]::GetEnvironmentVariable("DENOHOST_REQUIRE_METADATA_SIGNATURE"), "true", [StringComparison]::OrdinalIgnoreCase)
  $signaturePath = Join-Path (Split-Path $MetadataPath -Parent) "deno.metadata.sig"

  if ([string]::IsNullOrWhiteSpace($privateKeyPem)) {
    if ($requireSignature) {
      throw "Metadata signing key is required because DENOHOST_REQUIRE_METADATA_SIGNATURE=true. Configure DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM."
    }

    if (Test-Path $signaturePath) {
      Remove-Item $signaturePath -Force
    }

    Write-Warning "Metadata signing key is not configured (DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM). Signature file will not be generated."
    return
  }

  $metadataBytes = [System.IO.File]::ReadAllBytes($MetadataPath)
  $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
  try {
    $ecdsa.ImportFromPem($privateKeyPem)
    $signatureBytes = $ecdsa.SignData($metadataBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $signatureBase64 = [Convert]::ToBase64String($signatureBytes)
    [System.IO.File]::WriteAllText($signaturePath, $signatureBase64, [System.Text.UTF8Encoding]::new($false))
  } finally {
    $ecdsa.Dispose()
  }

  Write-Host "Wrote runtime metadata signature to $signaturePath"
}

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
  Write-ExecutableChecksum -Executable $ExecutablePath -OutputFile "$ExecutablePath.sha256sum"
  $resolvedRid = Resolve-RuntimeRid -Executable $ExecutablePath -ProvidedRuntimeRid $RuntimeRid
  $metadataPath = Write-RuntimeMetadata -Executable $ExecutablePath -Version $DenoVersion -Rid $resolvedRid -ArchiveName $DownloadFilename
  Sign-RuntimeMetadata -MetadataPath $metadataPath
  Write-Host "Deno setup complete at $ExecutablePath"
  exit 0
}

$downloadUrl = "https://github.com/denoland/deno/releases/download/v$($DenoVersion)/$($DownloadFilename)"
$checksumUrl = "$downloadUrl.sha256sum"
# Use unique temp file name to avoid conflicts when multiple projects build in parallel
$tempZip = "$env:TEMP\deno-$([System.Guid]::NewGuid().ToString('N').Substring(0,8)).zip"
$tempChecksum = "$tempZip.sha256sum"
$extractDir = Split-Path $ExecutablePath

Write-Host "Downloading Deno from $downloadUrl"
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing -ErrorAction Stop

Write-Host "Downloading checksum from $checksumUrl"
Invoke-WebRequest -Uri $checksumUrl -OutFile $tempChecksum -UseBasicParsing -ErrorAction Stop

$expectedHash = Get-ExpectedSha256 -ChecksumFile $tempChecksum -TargetFileName $DownloadFilename
$actualHash = (Get-FileHash -Algorithm SHA256 -Path $tempZip).Hash.ToLowerInvariant()

if ($expectedHash -ne $actualHash) {
  throw "Checksum verification failed for '$DownloadFilename'. Expected: $expectedHash Actual: $actualHash"
}

Write-Host "Checksum verification passed for $DownloadFilename"

Write-Host "Extracting to $extractDir"
Expand-Archive -Path $tempZip -DestinationPath $extractDir -Force

# Rename if necessary
$downloadedExe = Get-ChildItem -Path $extractDir -Recurse | Where-Object { $_.Name -like "deno*" -and !$_.PSIsContainer } | Select-Object -First 1
if ($downloadedExe -and $downloadedExe.FullName -ne $ExecutablePath) {
  Rename-Item -Path $downloadedExe.FullName -NewName (Split-Path $ExecutablePath -Leaf)
}

Write-ExecutableChecksum -Executable $ExecutablePath -OutputFile "$ExecutablePath.sha256sum"
$resolvedRid = Resolve-RuntimeRid -Executable $ExecutablePath -ProvidedRuntimeRid $RuntimeRid
$metadataPath = Write-RuntimeMetadata -Executable $ExecutablePath -Version $DenoVersion -Rid $resolvedRid -ArchiveName $DownloadFilename
Sign-RuntimeMetadata -MetadataPath $metadataPath

# Clean up temp file with retry logic
try {
  Remove-Item $tempZip -ErrorAction Stop
  if (Test-Path $tempChecksum) {
    Remove-Item $tempChecksum -ErrorAction Stop
  }
} catch {
  Write-Warning "Could not remove temporary download files ($tempZip / $tempChecksum): $($_.Exception.Message)"
}
Write-Host "Deno setup complete at $ExecutablePath"
