# Release Safety

This document is for maintainers and release automation.

## Metadata Signing

- Signing key input for build/runtime packaging: `DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM`
- Verification key is versioned in the repository as `Config/metadata-signing-public.pem` and embedded into `DenoHost.Core`.
- Runtime signature verification uses the bundled public key only.

Release/package builds must provide `DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM`; otherwise the workflow in `.github/workflows/build.yml` fails, and `DenoHost.Runtime.Downloader.DownloaderLogic` throws at runtime instead of silently skipping signing.

## Release Gates

1. Publish `vX.Y.Z-alpha.N` first (start with `.1`, increment if fixes are needed).
2. Validate CI, signing checks, and packaged smoke tests for the alpha.
3. Verify the published alpha from `nuget.org` using `DenoHost.Verify` (runtime startup + Deno version check).
4. Publish stable `vX.Y.Z` only from the exact same commit as `vX.Y.Z-alpha.N`.
5. Never publish if checksum bypass is enabled.

## Alpha Verification Against NuGet.org

- The workflow waits for NuGet propagation with retry/backoff before verification.
- Verification installs exact alpha versions from `nuget.org` and runs `DenoHost.Verify`.
- Stable/release continuation requires successful alpha verification.

## Break-Glass (Temporary Bypass)

For incident mitigation only, checksum validation can be bypassed by setting:

```bash
DENOHOST_ALLOW_CHECKSUM_BYPASS=true
```

Use this only as a short-term emergency workaround. Keep it disabled in normal operation.

Release paths in CI explicitly block publish when this variable is set.
