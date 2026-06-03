# generate-metadata-signing-keys

Creates an ECDSA signing key pair (PEM format) for runtime metadata signing.

Generated files:

- denohost-metadata-signing-private.pem
- denohost-metadata-signing-public.pem

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

Signing is skipped when DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is missing, but release/package builds should still provide the secret.

## Security Notes

- Never commit denohost-metadata-signing-private.pem.
- Keep the private key only in secure secret stores (for example GitHub Actions Secrets).
- Commit only the script and documentation, not generated key material.
- Rotate keys on a regular schedule and after any suspected exposure.

## Next Step

Commit denohost-metadata-signing-public.pem to Config/ and keep DenoHost.Core/Helper.cs embedding that file as the runtime verification source.
