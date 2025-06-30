param (
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$InstallDir = ".\"
)

$InstallDir = Resolve-Path -Path $InstallDir
$zipPath = Join-Path $env:TEMP "deno-$Version.zip"
$exeUrl = "https://github.com/denoland/deno/releases/download/v$Version/deno-x86_64-pc-windows-msvc.zip"

Write-Host "📥 Download Deno v$Version ZIP..."
Write-Host "🔗 URL: $exeUrl"

try {
    Invoke-WebRequest -Uri $exeUrl -OutFile $zipPath -UseBasicParsing
} catch {
    Write-Host "❌ Error during download. Check if the version exists: v$Version"
    exit 1
}

# Create target directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# ZIP unpack
Write-Host "📦 Extract ZIP to $InstallDir..."
Expand-Archive -Path $zipPath -DestinationPath $InstallDir -Force

# Cleanup
Remove-Item $zipPath -Force

# Testausgabe
$denoExe = Join-Path $InstallDir "deno.exe"
if (Test-Path $denoExe) {
    Write-Host "✅ Deno v$Version was successfully installed under:"
    Write-Host $denoExe
} else {
    Write-Host "⚠️ Installation failed: deno.exe not found"
}
