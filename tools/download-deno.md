# Deno Download

The runtime projects now call a shared .NET CLI implementation:

`DenoHost.Runtime.Downloader`

## Direct CLI Usage

**Example for linux-x64:**

```shell
dotnet ./DenoHost.Runtime.Downloader/bin/Debug/net9.0/DenoHost.Runtime.Downloader.dll \
  --executable-path "./DenoHost.Runtime.linux-x64/deno" \
  --download-filename "deno-x86_64-unknown-linux-gnu.zip" \
  --deno-version "2.8.1" \
  --runtime-rid "linux-x64"
```

## Build Integration

Each `DenoHost.Runtime.*` project builds the downloader CLI first and then executes it during `DownloadDenoIfMissing`.
