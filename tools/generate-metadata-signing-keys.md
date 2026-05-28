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
- DENOHOST_METADATA_SIGNING_PUBLIC_KEY_PEM <- content of denohost-metadata-signing-public.pem

## Runtime / Build Variables

- DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM: used during runtime package build to create deno.metadata.sig.
- DENOHOST_METADATA_SIGNING_PUBLIC_KEY_PEM: optional runtime override for signature verification key.

Signing is required: if DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM is missing, runtime package build fails.

## Security Notes

- Never commit denohost-metadata-signing-private.pem.
- Keep the private key only in secure secret stores (for example GitHub Actions Secrets).
- Commit only the script and documentation, not generated key material.
- Rotate keys on a regular schedule and after any suspected exposure.

## Next Step

Update BuiltInMetadataSigningPublicKeyPem in DenoHost.Core/Helper.cs with your generated public key if you want verification to work without environment overrides.
