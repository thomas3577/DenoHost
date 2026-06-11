# Deno Download

The runtime projects now call a shared .NET CLI implementation:

`DenoHost.Runtime.Downloader`

## Direct CLI Usage

**Example for linux-x64:**

```shell
export DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM="$(cat ./keys/denohost-metadata-signing-private.pem)"

dotnet ./DenoHost.Runtime.Downloader/bin/Debug/net9.0/DenoHost.Runtime.Downloader.dll \
  --executable-path "./DenoHost.Runtime.linux-x64/deno" \
  --download-filename "deno-x86_64-unknown-linux-gnu.zip" \
  --deno-version "2.8.1" \
  --runtime-rid "linux-x64"
```

## Build Integration

Each `DenoHost.Runtime.*` project builds the downloader CLI first and then executes it during `DownloadDenoIfMissing`.

## Local Build Helper (PowerShell)

For local builds that require metadata signing, use the helper script:

```powershell
pwsh -File .\tools\build-runtime-local.ps1 \
  -Project .\DenoHost.Runtime.win-x64\DenoHost.Runtime.win-x64.csproj \
  -Configuration Debug \
  -KeyFile .\keys\denohost-metadata-signing-private.pem
```

The script sets `DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM` only for the current process, runs `dotnet build`, and then restores the previous process value.
