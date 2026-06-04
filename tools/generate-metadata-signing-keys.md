# generate-metadata-signing-keys

Creates an ECDSA signing key pair (PEM format) for runtime metadata signing.

Generated files:

- denohost-metadata-signing-private.pem
- denohost-metadata-signing-public.pem (copy to Config/metadata-signing-public.pem for version control)

## Usage

Run from repository root:

```powershell
pwsh -File .\tools\generate-metadata-signing-keys.ps1 -OutputDirectory .\keys
```

Overwrite existing files:

```powershell
pwsh -File .\tools\generate-metadata-signing-keys.ps1 -OutputDirectory .\keys -Force
```

## Parameters

- OutputDirectory: target directory for generated PEM files. Default: current directory.
- Force: overwrite existing files.

## GitHub Secrets

Store file contents in repository secrets:

- DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM <- content of denohost-metadata-signing-private.pem

## Runtime / Build Variables

- DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM: used during runtime package build to create deno.metadata.sig.

Signing is required for release/package builds; if DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is missing, the build fails instead of skipping signing.

## Security Notes

- Never commit denohost-metadata-signing-private.pem.
- Keep the private key only in secure secret stores (for example GitHub Actions Secrets).
- Commit only the script and documentation, plus the verification key metadata-signing-public.pem in Config/ so DenoHost.Core/Helper.cs can embed it at runtime.
- Rotate keys on a regular schedule and after any suspected exposure.

## Next Step

Commit metadata-signing-public.pem to Config/ and keep DenoHost.Core/Helper.cs embedding that file as the runtime verification source.
