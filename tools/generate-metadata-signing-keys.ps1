param(
  [string]$OutputDirectory = ".",
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

$privatePath = Join-Path $OutputDirectory "denohost-metadata-signing-private.pem"
$publicPath = Join-Path $OutputDirectory "denohost-metadata-signing-public.pem"

if (-not (Test-Path $OutputDirectory)) {
  New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

if (-not $Force) {
  if (Test-Path $privatePath) {
    throw "File already exists: $privatePath. Use -Force to overwrite."
  }

  if (Test-Path $publicPath) {
    throw "File already exists: $publicPath. Use -Force to overwrite."
  }
}

$ecdsa = $null

# Prefer a P-256 key on Windows via CNG. If unavailable, fall back to portable creation.
if ($IsWindows) {
  try {
    $ecdsa = [System.Security.Cryptography.ECDsaCng]::new(256)
  }
  catch {
    $ecdsa = $null
  }
}

if ($null -eq $ecdsa) {
  try {
    $curve = [System.Security.Cryptography.ECCurve]::CreateFromFriendlyName("nistP256")
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create($curve)
  }
  catch {
    $ecdsa = $null
  }
}

if ($null -eq $ecdsa) {
  $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
}

try {
  $privatePem = $ecdsa.ExportECPrivateKeyPem()
  $publicPem = $ecdsa.ExportSubjectPublicKeyInfoPem()

  [System.IO.File]::WriteAllText($privatePath, $privatePem, [System.Text.UTF8Encoding]::new($false))
  [System.IO.File]::WriteAllText($publicPath, $publicPem, [System.Text.UTF8Encoding]::new($false))
}
finally {
  $ecdsa.Dispose()
}

Write-Host "Generated key files:"
Write-Host "  Private: $privatePath"
Write-Host "  Public : $publicPath"
Write-Host ""
Write-Host "GitHub Secrets:"
Write-Host "  DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM <- content of private pem file"
Write-Host ""
Write-Host "Next step: commit the generated public key to Config/metadata-signing-public.pem."
