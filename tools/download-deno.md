# Deno Download

## via PowerShell

**For win-x64:**

```shell
$filename = ".\\tools\\download-deno.ps1"
$denoExecutable = ".\\DenoHost.Runtime.win-x64\\deno.exe"
$denoDownloadFilename = "deno-x86_64-pc-windows-msvc.zip"
$denoVersion = "2.5.6"
pwsh -NoProfile -ExecutionPolicy Bypass -File $filename -ExecutablePath $DenoExecutable -DownloadFilename $denoDownloadFilename -DenoVersion $denoVersion
```

## via Bash

**For linux-x64:**

```shell
FILENAME="./tools/download-deno.sh"
DENO_EXECUTABLE_PATH="./DenoHost.Runtime.linux-x64/deno"
DENO_DOWNLOAD_FILENAME="deno-x86_64-unknown-linux-gnu.zip"
DENO_VERSION="2.5.6"
bash $FILENAME $DENO_EXECUTABLE_PATH $DENO_DOWNLOAD_FILENAME $DENO_VERSION
```
