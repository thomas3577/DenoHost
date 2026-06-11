# Verify Alpha NuGet

Composite action for the maintainer-only alpha verification job.

## Inputs

- `package-version`: Alpha package version to verify.
- `deno-version`: Expected Deno runtime version reported by the published package.
- `runtime-package-id`: Runtime package id to verify. Defaults to `DenoHost.Runtime.linux-x64`.

## What it does

1. Waits for the alpha packages to become visible on nuget.org.
2. Restores `DenoHost.Verify` against the matching alpha packages.
3. Runs the verify app and checks the Deno version exposed by the runtime package.
