param(
  [string]$Project = ".\DenoHost.Runtime.win-x64\DenoHost.Runtime.win-x64.csproj",
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",
  [string]$KeyFile = ".\keys\denohost-metadata-signing-private.pem"
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not [System.IO.Path]::IsPathRooted($Project)) {
  $Project = [System.IO.Path]::GetFullPath($Project, $repoRoot)
}

if (-not [System.IO.Path]::IsPathRooted($KeyFile)) {
  $KeyFile = [System.IO.Path]::GetFullPath($KeyFile, $repoRoot)
}

if (-not (Test-Path $Project)) {
  throw "Project not found: $Project"
}

if (-not (Test-Path $KeyFile)) {
  throw "Signing key file not found: $KeyFile"
}

$previousValue = [Environment]::GetEnvironmentVariable("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM", "Process")

try {
  $privatePem = Get-Content $KeyFile -Raw
  if ([string]::IsNullOrWhiteSpace($privatePem)) {
    throw "Signing key file is empty: $KeyFile"
  }

  [Environment]::SetEnvironmentVariable("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM", $privatePem, "Process")

  Write-Host "Building $Project ($Configuration) with signing key from $KeyFile"
  dotnet build $Project -c $Configuration

  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
finally {
  [Environment]::SetEnvironmentVariable("DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM", $previousValue, "Process")
}
